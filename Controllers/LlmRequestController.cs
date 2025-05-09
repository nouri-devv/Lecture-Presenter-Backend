/* Achived, no longer used
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
            var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
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

    [HttpPost("structured-explanation")]
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

            // Teaching-focused prompt
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
*/