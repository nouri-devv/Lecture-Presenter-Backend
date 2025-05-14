using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Minio;
using Minio.DataModel.Args;

[ApiController]
[Authorize]
[Route("api/audios")]
public class AudioController : ControllerBase
{
    private readonly IMinioClient _minioClient;
    private const string Bucket = "storage";

    public AudioController(IMinioClient minioClient)
    {
        _minioClient = minioClient;
    }

    [HttpGet("{sessionId}/audio/{audioNumber}")]
    public async Task<IActionResult> UploadAudio(string sessionId, int audioNumber)
    {
        string formattedAudioNumber = audioNumber.ToString("D3");
        string objectPath = $"sessions/{sessionId}/audios/audio_{formattedAudioNumber}.mp3";

        bool exists = await ObjectExistsAsync(Bucket, objectPath);
        if (!exists)
            return NotFound($"Audio {audioNumber} not found in session {sessionId}");

        using var memoryStream = new MemoryStream();
        await _minioClient.GetObjectAsync(new GetObjectArgs()
            .WithBucket(Bucket)
            .WithObject(objectPath)
            .WithCallbackStream(stream => stream.CopyTo(memoryStream)));

        memoryStream.Position = 0;
        return File(memoryStream.ToArray(), "audio/mpeg");
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