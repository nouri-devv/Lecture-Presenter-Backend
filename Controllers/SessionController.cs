using Microsoft.AspNetCore.Mvc;
using Minio;
using Minio.DataModel.Args;
using PdfSharpCore.Pdf.IO;



[ApiController]
[Route("api/new-session")]

public class UploadController : ControllerBase
{
    private static readonly List<SessionRecord> _SessionRecord = new List<SessionRecord>();
    private readonly IMinioClient _minioClient;
    private const string BucketName = "storage";

    public UploadController(IMinioClient minioClient)
    {
        _minioClient = minioClient;
    }

    private string GetProjectRoot()
    {
        var dir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(dir, "..", "..", "..")); // assumes standard bin/Debug/netX
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


    [HttpPost]
    public async Task<IActionResult> CreateFileRecord(IFormFile file)
    {
        if (file == null)
            return BadRequest("File record cannot be null.");

        // Generate session and collection IDs
        string sessionId = GenerateID.GenerateRandomId();

        var bucketStructure = $"sessions/{sessionId}";

        var slideRecords = await HandleSlideRecords(file, bucketStructure);

        var newSessionRecord = new SessionRecord(
            sessionId,
            DateTime.UtcNow,
            DateTime.UtcNow,
            slideRecords
        );

        _SessionRecord.Add(newSessionRecord);

        return Ok(newSessionRecord);
    }
}