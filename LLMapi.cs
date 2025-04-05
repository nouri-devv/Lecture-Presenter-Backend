using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using DotNetEnv;

[Route("api/LLMapi")]
[ApiController]
public class LLMapi : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public LLMapi()
    {
        _httpClient = new HttpClient();
        DotNetEnv.Env.Load();
        _apiKey = Environment.GetEnvironmentVariable("api_key")?.Trim() ?? 
            throw new InvalidOperationException("API key not found in environment variables");
    }

    [HttpGet("file")]
    public async Task<IActionResult> getFile([FromQuery] string fileName)
    {
        if (fileName == null)
            return BadRequest("No file name provided.");

        string filePath = Path.Combine(Directory.GetCurrentDirectory(), "UploadedFiles", fileName);
        if (!System.IO.File.Exists(filePath))
            return BadRequest("File does not exist.");

        try
        {
            string fileContent = await System.IO.File.ReadAllTextAsync(filePath);

            var requestData = new
            {
                model = "gemma2-9b-it",
                messages = new[]
                {
                    new { role = "system", content = "Sum this up." },
                    new { role = "user", content = fileContent }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestData),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, responseContent);
            }

            // Parse the response to get just the content
            using JsonDocument doc = JsonDocument.Parse(responseContent);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return Ok(content);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}