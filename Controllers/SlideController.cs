using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Minio;
using Minio.DataModel.Args;

[ApiController]
[Authorize]
[Route("api/slides")]
public class SlideController : ControllerBase
{
    private readonly IMinioClient _minioClient;
    private const string SlidesBucketName = "storage";

    public SlideController(IMinioClient minioClient)
    {
        _minioClient = minioClient;
    }

    [HttpGet("{sessionId}/slide/{slideNumber}")]
    public async Task<IActionResult> GetSlide(string sessionId, int slideNumber)
    {
        try
        {
            // Format slide number with leading zeros (001, 002, etc.)
            string formattedSlideNumber = slideNumber.ToString("D3");
            string objectPath = $"sessions/{sessionId}/slides/slide_{formattedSlideNumber}.png";

            bool exists = await ObjectExistsAsync(SlidesBucketName, objectPath);
            if (!exists)
                return NotFound($"Slide {slideNumber} not found in session {sessionId}");

            using var memoryStream = new MemoryStream();
            await _minioClient.GetObjectAsync(new GetObjectArgs()
                .WithBucket(SlidesBucketName)
                .WithObject(objectPath)
                .WithCallbackStream(stream => stream.CopyTo(memoryStream)));

            memoryStream.Position = 0;
            return File(memoryStream.ToArray(), "image/png");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving slide: {ex.Message}");
        }
    }

    // Helper method to check if an object exists in MinIO
    private async Task<bool> ObjectExistsAsync(string bucketName, string objectName)
    {
        try
        {
            await _minioClient.StatObjectAsync(new StatObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName));
            return true;
        }
        catch
        {
            return false;
        }
    }
}