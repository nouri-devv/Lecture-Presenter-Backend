using Microsoft.AspNetCore.Mvc;
using Minio;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Text;
using Minio.DataModel.Args;
using Sprache;
using System.Runtime.InteropServices.JavaScript;

[ApiController]
[Route("api/new-session")]
public class SessionController : ControllerBase
{
    private readonly ISessionDataAccess _sessionRepo;
    private readonly ISlideDataAccess _slideRepo;
    private readonly LlmResponseRecordDataAccess _llmResponseRepo;
    private readonly IAudioDataAccess _audioRepo;
    private readonly IMinioClient _minioClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private const string BucketName = "storage";

    public SessionController(
        ISessionDataAccess sessionRepo,
        ISlideDataAccess slideRepo,
        LlmResponseRecordDataAccess llmResponseRepo,
        IAudioDataAccess audioRepo,
        IMinioClient minioClient,
        IHttpClientFactory httpClientFactory)
    {
        _sessionRepo = sessionRepo ?? throw new ArgumentNullException(nameof(sessionRepo));
        _slideRepo = slideRepo ?? throw new ArgumentNullException(nameof(slideRepo));
        _llmResponseRepo = llmResponseRepo ?? throw new ArgumentNullException(nameof(llmResponseRepo));
        _audioRepo = audioRepo ?? throw new ArgumentNullException(nameof(audioRepo));
        _minioClient = minioClient ?? throw new ArgumentNullException(nameof(minioClient));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    [HttpPost]
    public async Task<IActionResult> CreateSession(IFormFile file)
    {
        try
        {
            if (file == null)
                return BadRequest("File record cannot be null.");

            // Read file content once
            byte[] fileContent;
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                fileContent = memoryStream.ToArray();
            }

            // Generate unique identifiers for the session
            var sessionId = Guid.NewGuid().ToString();
            var bucketStructure = $"sessions/{sessionId}";

            // Create session in database
            _sessionRepo.AddSession(sessionId, DateTime.UtcNow, DateTime.UtcNow);

            // Process slides and LLM responses in parallel
            var slideTask = HandleSlideRecords(fileContent, bucketStructure, sessionId);
            var llmTask = HandleLlmResponseRecords(fileContent, sessionId);

            await Task.WhenAll(slideTask, llmTask);

            var slideRecords = await slideTask;
            var llmResponseRecords = await llmTask;

            // Process audio files
            var audioRecords = await HandleAudioRecords(llmResponseRecords, bucketStructure, sessionId);

            var response = new
            {
                SessionId = sessionId,
                SlideRecords = slideRecords,
                LlmResponseRecords = llmResponseRecords,
                AudioRecords = audioRecords
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating file record: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");

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

    private async Task<List<SlideRecord>> HandleSlideRecords(byte[] fileContent, string bucketStructure, string sessionId)
    {
        var response = new List<SlideRecord>();
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string tempPdfPath = Path.Combine(tempDir, "upload.pdf");

        try
        {
            // Write the file content to disk
            await System.IO.File.WriteAllBytesAsync(tempPdfPath, fileContent);

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
            var parsedSlideRecords = System.Text.Json.JsonSerializer.Deserialize<List<SlideRecord>>(stdout, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsedSlideRecords == null)
                throw new Exception("Failed to deserialize slide records from Python script.");

            // Save each slide record to the database
            foreach (var slide in parsedSlideRecords)
            {
                var slideRecord = new SlideRecord
                {
                    SlideId = slide.SlideId,
                    SessionId = sessionId,
                    SlideNumber = slide.SlideNumber,
                    SlideLocation = slide.SlideLocation
                };
                _slideRepo.CreateSlide(slideRecord);
                response.Add(slideRecord);
            }
        }
        finally
        {
            // Clean up
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }

        return response;
    }

    private async Task<List<LlmResponseRecord>> HandleLlmResponseRecords(byte[] fileContent, string sessionId)
    {
        var base64File = Convert.ToBase64String(fileContent);

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

                        var llmResponse = new LlmResponseRecord
                        {
                            LlmResponseId = Guid.NewGuid().ToString(),
                            SessionId = sessionId,
                            LlmResponseNumber = pageNumber,
                            LlmResponseHeading = heading,
                            LlmResponseExplanation = explanation
                        };

                        // Save to database
                        var savedResponse = _llmResponseRepo.AddLlmResponseRecord(llmResponse);
                        result.Add(savedResponse);
                    }
                }
            }
            else
            {
                // Add a fallback response record
                var fallbackResponse = new LlmResponseRecord
                {
                    LlmResponseId = Guid.NewGuid().ToString(),
                    SessionId = sessionId,
                    LlmResponseNumber = 1,
                    LlmResponseHeading = "API Response Format Error",
                    LlmResponseExplanation = "The system encountered an unexpected response format from the AI service. Please try again later."
                };

                // Save to database
                var savedFallback = _llmResponseRepo.AddLlmResponseRecord(fallbackResponse);
                result.Add(savedFallback);
            }
        }
        catch (Exception ex)
        {
            // Add an error response record
            var errorResponse = new LlmResponseRecord
            {
                LlmResponseId = Guid.NewGuid().ToString(),
                SessionId = sessionId,
                LlmResponseNumber = 1,
                LlmResponseHeading = "Processing Error",
                LlmResponseExplanation = "An error occurred while processing the document. Please try again later."
            };

            // Save to database
            var savedError = _llmResponseRepo.AddLlmResponseRecord(errorResponse);
            result.Add(savedError);
        }

        return result;
    }

    private async Task<List<AudioRecord>> HandleAudioRecords(List<LlmResponseRecord> llmResponses, string bucketStructure, string sessionId)
    {
        var audioTasks = new List<Task<AudioRecord>>();

        foreach (var resp in llmResponses)
        {
            string explanation = !string.IsNullOrEmpty(resp.LlmResponseExplanation)
                ? resp.LlmResponseExplanation
                : "No explanation available for this section.";

            audioTasks.Add(GenerateAndStoreTtsAudio(explanation, resp.LlmResponseNumber, bucketStructure, sessionId));
        }

        // Convert the result of Task.WhenAll to a List<AudioRecord>
        var audioRecordsArray = await Task.WhenAll(audioTasks);
        return audioRecordsArray.ToList();  // Convert array to List<AudioRecord>
    }

    private async Task<AudioRecord> GenerateAndStoreTtsAudio(string text, int recordNumber, string bucketStructure, string sessionId)
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

                    // Create audio record
                    var audioRecord = new AudioRecord
                    {
                        AudioRecordId = Guid.NewGuid().ToString(),
                        SessionId = sessionId,
                        AudioRecordNumber = recordNumber,
                        AudioRecordLocation = objectName
                    };

                    // Save to database
                    return _audioRepo.AddAudioRecord(audioRecord);
                }
            }

            // If we get here, something went wrong with the response format
            throw new Exception("Invalid TTS API response format");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing TTS response: {ex.Message}");
            Console.WriteLine($"Response body: {responseBody}");

            // Create placeholder record with error information
            var errorAudioRecord = new AudioRecord
            {
                AudioRecordId = Guid.NewGuid().ToString(),
                SessionId = sessionId,
                AudioRecordNumber = recordNumber,
                AudioRecordLocation = "Error: Unable to generate audio"
            };

            // Save to database even if it's an error record
            return _audioRepo.AddAudioRecord(errorAudioRecord);
        }
    }
}