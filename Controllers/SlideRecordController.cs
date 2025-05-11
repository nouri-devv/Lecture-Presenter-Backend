using Microsoft.AspNetCore.Mvc;
using Minio;
using Minio.DataModel.Args;


[ApiController]
[Route("api/slide-record")]
public class SlideRecordController : ControllerBase
{
    private static readonly List<SessionRecord> _SessionRecord = new List<SessionRecord>();
    private readonly IMinioClient _minioClient;
    private const string SlidesBucketName = "storage";

    public SlideRecordController(IMinioClient minioClient)
    {
        _minioClient = minioClient;
    }
    /*
        [HttpGet("{sessionId}/slides/{slideNumber}", Name = "GetSlideRecordById")]
        public async Task<IActionResult> GetSlideRecordById(string sessionId, int slideNumber)
        {
            try
            {
                string objectName = slide.SlideLocation;

                // Check if the object exists in MinIO before attempting to retrieve it
                bool exists = await ObjectExistsAsync(SlidesBucketName, objectName);
                if (!exists)
                    return NotFound($"Slide file not found in storage: {objectName}");

                using var memoryStream = new MemoryStream();
                await _minioClient.GetObjectAsync(new GetObjectArgs()
                    .WithBucket(SlidesBucketName)
                    .WithObject(objectName)
                    .WithCallbackStream(stream => stream.CopyTo(memoryStream)));

                memoryStream.Position = 0;
                return File(memoryStream.ToArray(), "image/png");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error retrieving slide: {ex.Message}");
            }
        }
    */

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
        catch (Exception)
        {
            return false;
        }
    }
}