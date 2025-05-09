using Microsoft.AspNetCore.Mvc;
using Minio;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Text;

[ApiController]
[Route("api/new-session")]
public class UploadController : ControllerBase
{
    private static readonly List<SessionRecord> _SessionRecord = new List<SessionRecord>();
    private readonly IMinioClient _minioClient;
    private const string BucketName = "storage";
    private readonly IHttpClientFactory _httpClientFactory;


    public UploadController(IMinioClient minioClient, IHttpClientFactory httpClientFactory)
    {
        _minioClient = minioClient;
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    private string GetProjectRoot()
    {
        var dir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(dir, "..", "..", ".."));
    }

    async Task<List<SlideRecord>> HandleSlideRecords(IFormFile file, string bucketStructure)
    {
        var slideRecords = new List<SlideRecord>();

        // Create temporary directory and save the uploaded file
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string tempPdfPath = Path.Combine(tempDir, "upload.pdf");

        try
        {
            // Save the uploaded file to disk
            using (var fileStream = new FileStream(tempPdfPath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            // Run the Python script
            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "python3",
                Arguments = $"convert_and_upload.py \"{tempPdfPath}\" \"{bucketStructure.Split('/').Last()}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = GetProjectRoot()
            };

            using var process = System.Diagnostics.Process.Start(processStartInfo);
            string stdout = await process.StandardOutput.ReadToEndAsync();
            string stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new Exception($"Python script failed with exit code {process.ExitCode}:\n{stderr}");
            }

            // Parse JSON from stdout
            slideRecords = System.Text.Json.JsonSerializer.Deserialize<List<SlideRecord>>(stdout, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (slideRecords == null)
                throw new Exception("Failed to deserialize slide records from Python script.");
        }
        finally
        {
            // Clean up
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }

        return slideRecords;
    }

    async Task<List<LlmResponseRecord>> HandleLlmResponseRecords(IFormFile file, string bucketStructure)
    {
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        var fileBytes = memoryStream.ToArray();
        var base64File = Convert.ToBase64String(fileBytes);

        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";

        var toolDefinition = new
        {
            function_declarations = new[]
            {
            new
            {
                name = "teachPage",
                description = "Explain the content of a specific page like a university lecturer teaching students. Focus on clarity, breakdown of ideas, and pacing for audio delivery.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        pageNumber = new { type = "integer", description = "Page number in the document" },
                        heading = new { type = "string", description = "Main topic or heading of the page" },
                        explanation = new { type = "string", description = "Detailed but clear explanation suitable for student listening (e.g., audio narration)" }
                    },
                    required = new[] { "pageNumber", "heading", "explanation" }
                }
            }
        }
        };

        var requestBody = new
        {
            contents = new[]
            {
            new
            {
                role = "user",
                parts = new object[]
                {
                    new
                    {
                        text = @"You are a university lecturer creating audio explanations for course material.

Go through each page of the attached PDF document.

For **each page**, use the function `teachPage` to:
- Identify the **page number**
- Extract or infer the **main topic or heading**
- Provide a **clear, structured explanation** of the content as if you are teaching it out loud to students.

Use simple language, examples, and teaching analogies where possible. Your tone should be **engaging**, **educational**, and **paced for listening**.

Do **not summarize**. Instead, **explain** the ideas — break down the content and help students truly understand the material.

Assume this will be **used for audio narration**."
                    },
                    new
                    {
                        inlineData = new
                        {
                            mimeType = "application/pdf",
                            data = base64File
                        }
                    }
                }
            }
        },
            tools = new[] { toolDefinition }
        };

        var client = _httpClientFactory.CreateClient();
        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var httpContent = new StringContent(json, Encoding.UTF8);
        httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var response = await client.PostAsync(url, httpContent);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"LLM API request failed: {responseContent}");

        // Parse Gemini response
        var parsed = JsonSerializer.Deserialize<JsonElement>(responseContent);

        var parts = parsed
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts");

        var result = new List<LlmResponseRecord>();

        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("functionCall", out JsonElement functionCall) &&
                functionCall.GetProperty("name").GetString() == "teachPage")
            {
                var args = functionCall.GetProperty("args");

                var heading = args.TryGetProperty("heading", out var h) ? h.GetString() : "";
                var explanation = args.TryGetProperty("explanation", out var e) ? e.GetString() : "";
                var pageNumber = args.TryGetProperty("pageNumber", out var p) ? p.GetInt32() : 0;

                result.Add(new LlmResponseRecord(
                    responseId: Guid.NewGuid().ToString(),
                    llmReponseNumber: pageNumber,
                    responseHeading: heading,
                    responseExplanation: explanation
                ));
            }
        }

        return result;
    }

    [HttpPost]
    public async Task<IActionResult> CreateFileRecord(IFormFile file)
    {
        if (file == null)
            return BadRequest("File record cannot be null.");

        string sessionId = GenerateID.GenerateRandomId();
        var bucketStructure = $"sessions/{sessionId}";

        // Start both tasks
        var slideTask = HandleSlideRecords(file, bucketStructure);
        var llmTask = HandleLlmResponseRecords(file, bucketStructure);

        // Wait for both to complete
        await Task.WhenAll(slideTask, llmTask);

        var slideRecords = await slideTask;
        var llmResponseRecords = await llmTask;

        var newSessionRecord = new SessionRecord(
            sessionId,
            DateTime.UtcNow,
            DateTime.UtcNow,
            slideRecords,
            llmResponseRecords
        );

        _SessionRecord.Add(newSessionRecord);

        return Ok(newSessionRecord);
    }
}