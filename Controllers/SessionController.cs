using Microsoft.AspNetCore.Mvc;
using Minio;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Text;
using Minio.DataModel.Args;

[ApiController]
[Route("api/new-session")]
public class SessionController : ControllerBase
{
    private static readonly List<SessionRecord> _SessionRecord = new List<SessionRecord>();
    private readonly IMinioClient _minioClient;
    private const string BucketName = "storage";
    private readonly IHttpClientFactory _httpClientFactory;


    public SessionController(IMinioClient minioClient, IHttpClientFactory httpClientFactory)
    {
        _minioClient = minioClient;
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    [HttpPost]
    public async Task<IActionResult> CreateNewSession(IFormFile file)
    {
        try
        {
            if (file == null)
                return BadRequest("File record cannot be null.");

            string sessionId = GenerateID.GenerateRandomId();
            var bucketStructure = $"sessions/{sessionId}";

            // Start both tasks
            var slideTask = HandleSlideRecords(file, bucketStructure);
            var llmTask = HandleLlmResponseRecords(file);

            // Wait for both to complete
            await Task.WhenAll(slideTask, llmTask);

            var slideRecords = await slideTask;
            var llmResponseRecords = await llmTask;

            var audioTasks = new List<Task<AudioRecord>>();
            foreach (var resp in llmResponseRecords)
            {
                // Ensure the explanation is not null or empty before sending to TTS
                string explanation = !string.IsNullOrEmpty(resp.ResponseExplanation)
                    ? resp.ResponseExplanation
                    : "No explanation available for this section.";

                audioTasks.Add(GenerateAndStoreTtsAudio(explanation, resp.LlmReponseNumber, bucketStructure));
            }

            var audioRecords = await Task.WhenAll(audioTasks);

            var newSessionRecord = new SessionRecord(
                sessionId,
                DateTime.UtcNow,
                DateTime.UtcNow,
                slideRecords,
                llmResponseRecords,
                audioRecords.ToList()
            );

            _SessionRecord.Add(newSessionRecord);

            return Ok(newSessionRecord);
        }
        catch (Exception ex)
        {
            // Log the error
            Console.WriteLine($"Error creating file record: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");

            // Return detailed error for debugging
            return StatusCode(500, new
            {
                error = "An error occurred while processing the file",
                message = ex.Message,
                stackTrace = ex.StackTrace
            });
        }
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

    async Task<List<LlmResponseRecord>> HandleLlmResponseRecords(IFormFile file)
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

        var result = new List<LlmResponseRecord>();

        try
        {
            // Parse Gemini response with safe access to properties
            var parsed = JsonSerializer.Deserialize<JsonElement>(responseContent);

            // Check if the response contains the expected structure
            if (parsed.TryGetProperty("candidates", out var candidates) &&
                candidates.GetArrayLength() > 0 &&
                candidates[0].TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts))
            {
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("functionCall", out JsonElement functionCall) &&
                        functionCall.TryGetProperty("name", out var name) &&
                        name.GetString() == "teachPage" &&
                        functionCall.TryGetProperty("args", out JsonElement args))
                    {
                        // Extract values safely
                        var heading = args.TryGetProperty("heading", out var h) ? h.GetString() : "Untitled";
                        var explanation = args.TryGetProperty("explanation", out var e) ? e.GetString() : "No explanation provided";
                        var pageNumber = args.TryGetProperty("pageNumber", out var p) && p.TryGetInt32(out int pageNum) ? pageNum : 0;

                        result.Add(new LlmResponseRecord(
                            responseId: Guid.NewGuid().ToString(),
                            llmReponseNumber: pageNumber,
                            responseHeading: heading,
                            responseExplanation: explanation
                        ));
                    }
                }
            }
            else
            {
                // Handle unexpected response format
                Console.WriteLine($"Unexpected API response format: {responseContent}");

                // Add a fallback response record
                result.Add(new LlmResponseRecord(
                    responseId: Guid.NewGuid().ToString(),
                    llmReponseNumber: 1,
                    responseHeading: "API Response Format Error",
                    responseExplanation: "The system encountered an unexpected response format from the AI service. Please try again later."
                ));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing LLM response: {ex.Message}");
            Console.WriteLine($"Response content: {responseContent}");

            // Add an error response record
            result.Add(new LlmResponseRecord(
                responseId: Guid.NewGuid().ToString(),
                llmReponseNumber: 1,
                responseHeading: "Processing Error",
                responseExplanation: "An error occurred while processing the document. Please try again later."
            ));
        }

        // Ensure we have at least one record
        if (result.Count == 0)
        {
            result.Add(new LlmResponseRecord(
                responseId: Guid.NewGuid().ToString(),
                llmReponseNumber: 1,
                responseHeading: "No Content Generated",
                responseExplanation: "No content could be generated from the document. Please check the file and try again."
            ));
        }

        return result;
    }

    private async Task<AudioRecord> GenerateAndStoreTtsAudio(string text, int recordNumber, string bucketStructure)
    {
        var apiKey = Environment.GetEnvironmentVariable("TEXT_TO_SPEECH_API_KEY");
        var url = $"https://texttospeech.googleapis.com/v1/text:synthesize?key={apiKey}";

        var requestBody = new
        {
            input = new { text },
            voice = new { languageCode = "en-US", name = "en-US-Wavenet-D" },
            audioConfig = new { audioEncoding = "MP3" }
        };

        var client = _httpClientFactory.CreateClient();
        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await client.PostAsync(url, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"TTS API request failed: {responseBody}");

        try
        {
            var responseJson = JsonSerializer.Deserialize<JsonElement>(responseBody);

            // Safely access the audioContent property
            if (responseJson.TryGetProperty("audioContent", out var audioContentElement) &&
                audioContentElement.ValueKind == JsonValueKind.String)
            {
                var audioBase64 = audioContentElement.GetString();
                if (!string.IsNullOrEmpty(audioBase64))
                {
                    var audioBytes = Convert.FromBase64String(audioBase64);

                    string audioFileName = $"audio_{recordNumber}.mp3";
                    string objectName = $"{bucketStructure}/audio/{audioFileName}";

                    using var audioStream = new MemoryStream(audioBytes);

                    var putObjectArgs = new PutObjectArgs()
                        .WithBucket(BucketName)
                        .WithObject(objectName)
                        .WithStreamData(audioStream)
                        .WithObjectSize(audioBytes.Length)
                        .WithContentType("audio/mpeg");
                    await _minioClient.PutObjectAsync(putObjectArgs);

                    return new AudioRecord(
                        audioRecordId: Guid.NewGuid().ToString(),
                        audioRecordLocation: objectName
                    )
                    {
                        AudioRecordNumber = recordNumber
                    };
                }
            }

            // If we get here, something went wrong with the response format
            throw new Exception("Invalid TTS API response format");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing TTS response: {ex.Message}");
            Console.WriteLine($"Response body: {responseBody}");

            // Return a placeholder record with error information
            return new AudioRecord(
                audioRecordId: Guid.NewGuid().ToString(),
                audioRecordLocation: $"error-{recordNumber}"
            )
            {
                AudioRecordNumber = recordNumber
            };
        }
    }
}