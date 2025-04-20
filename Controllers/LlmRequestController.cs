using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;

[ApiController]
[Route("api/llm-request")]
public class LLMRequestController : ControllerBase
{
    // These are used to make HTTP requests, log errors, and read configuration settings
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LLMRequestController> _logger;
    private readonly IConfiguration _configuration;

    // Constructor - sets up the controller with necessary services
    public LLMRequestController(IHttpClientFactory httpClientFactory, ILogger<LLMRequestController> logger, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpPost()]
    public async Task<IActionResult> GenerateContent([FromBody] GenerateContentRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request?.Text))
                return BadRequest(new { error = "Text content is required" });

            // Set up the HTTP client and API details
            var client = _httpClientFactory.CreateClient();
            var apiKey = _configuration["Gemini:ApiKey"];
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={apiKey}";

            // Prepare the request body in the format Gemini API expects
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[]
                        {
                            new { text = request.Text }
                        }
                    }
                }
            };

            // Convert the request body to JSON
            var jsonContent = System.Text.Json.JsonSerializer.Serialize(requestBody, 
                new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(url, httpContent);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                return Ok(responseContent);

            // If there's an API error, log it and return the error details
            _logger.LogError("Gemini API error: {StatusCode}, Response: {Response}", response.StatusCode, responseContent);
            return StatusCode((int)response.StatusCode, new
            {
                error = "Error calling Gemini API",
                details = responseContent,
                statusCode = (int)response.StatusCode
            });
        }
        catch (Exception ex)
        {
            // If something unexpected happens, log the error and return a 500 response
            _logger.LogError(ex, "Unexpected error while calling Gemini API");
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