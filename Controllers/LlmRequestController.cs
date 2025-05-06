using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;

[ApiController]
[Route("api/llm-request")]
public class LlmRequestController : ControllerBase
{
    // These are used to make HTTP requests, log errors, and read configuration settings
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LlmRequestController> _logger;

    // Constructor - sets up the controller with necessary services
    public LlmRequestController(IHttpClientFactory httpClientFactory, ILogger<LlmRequestController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> GenerateContentWithFile([FromForm] IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file uploaded." });

            // Read the file into a byte array
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();
            var base64File = Convert.ToBase64String(fileBytes);

            // Build the Gemini API request
            var apiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-pro:generateContent?key={apiKey}";

            var requestBody = new
            {
                contents = new[]
                {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = "Please summarize this PDF document." },
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
            }
            };

            var client = _httpClientFactory.CreateClient();
            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, httpContent);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                return Ok(responseContent);

            _logger.LogError("Gemini API error: {StatusCode}, Response: {Response}", response.StatusCode, responseContent);
            return StatusCode((int)response.StatusCode, new
            {
                error = "Error calling Gemini API",
                details = responseContent
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while calling Gemini API with file");
            return StatusCode(500, new
            {
                error = "Internal server error",
                details = ex.Message
            });
        }
    }

    [HttpPost("structured-summary")]
    public async Task<IActionResult> GenerateStructuredSummary([FromForm] IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file uploaded." });

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();
            var base64File = Convert.ToBase64String(fileBytes);

            var apiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-pro:generateContent?key={apiKey}";

            // Define the function (tool) for structured summary
            var toolDefinition = new
            {
                function_declarations = new[]
                {
                new
                {
                    name = "summarizePage",
                    description = "Expain the content of the specific page as if you were a professor and teaching to you students. Make sure it is concise and clear.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            pageNumber = new { type = "integer", description = "Page number of the document" },
                            heading = new { type = "string", description = "Title or heading of the page" },
                            summary = new { type = "string", description = "Short summary of the page content" }
                        },
                        required = new[] { "pageNumber", "heading", "summary" }
                    }
                }
            }
            };

            // Prepare the request body
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
                            text = @"You are a university lecturer explaining course material to students.
                            For the attached PDF document, go through **each page one by one**.
                            For each page, use the function `summarizePage` to:
                            - Identify the **page number**
                            - Extract or infer the **heading** or main topic
                            - Write a **clear, concise explanation** of the key concepts as if teaching a class.

                            Avoid just summarizing. Instead, explain the **concepts**, break them down, and use simple teaching language.
                            Give me the summary for each page as if the text is going to be used for audio narration.
                            Make sure the explanations are **concise** and **clear** for audio narration.
                            Ensure that each page is long enought to be used for audio narration."
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

            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, httpContent);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                return Ok(JsonDocument.Parse(responseContent));

            _logger.LogError("Gemini API error: {StatusCode}, Response: {Response}", response.StatusCode, responseContent);
            return StatusCode((int)response.StatusCode, new
            {
                error = "Error calling Gemini API",
                details = responseContent
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during structured summary");
            return StatusCode(500, new
            {
                error = "Internal server error",
                details = ex.Message
            });
        }
    }

}

// Simple class to hold the text that will be sent to the AI
public class GenerateContentRequest
{
    public string Text { get; set; }
}