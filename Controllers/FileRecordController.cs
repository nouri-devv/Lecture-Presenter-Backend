using Microsoft.AspNetCore.Mvc;
using PdfSharpCore.Pdf.IO;
using Minio;
using Minio.DataModel.Args;

[ApiController]
[Route("api/file-record")]
public class FileRecordController : ControllerBase
{
    private readonly IMinioClient _minioClient;
    private const string BucketName = "lecture-files";
    private const string SlidesBucketName = "lecture-slides";

    private static readonly List<FileRecord> _FilesRecords = new List<FileRecord>();

    public FileRecordController(IMinioClient minioClient)
    {
        _minioClient = minioClient;
    }

    [HttpGet("{id}", Name = "GetFileRecordById")]
    public IActionResult GetFileRecordById(string id)
    {
        var fileRecord = _FilesRecords.FirstOrDefault(fr => fr.FileId == id);
        if (fileRecord == null)
            return NotFound();

        return Ok(fileRecord);
    }

    [HttpPost]
    public async Task<IActionResult> CreateFileRecord(IFormFile file)
    {
        if (file == null)
            return BadRequest("File record cannot be null.");

        // Generate a unique object name for MinIO
        string objectNameAndId = GenerateID.GenerateRandomId();

        try
        {
            // Ensure bucket exists
            var bucketExistsArgs = new BucketExistsArgs()
                .WithBucket(BucketName);
            bool bucketExists = await _minioClient.BucketExistsAsync(bucketExistsArgs);

            if (!bucketExists)
            {
                var makeBucketArgs = new MakeBucketArgs()
                    .WithBucket(BucketName);
                await _minioClient.MakeBucketAsync(makeBucketArgs);
            }

            // Upload file to MinIO
            using (var stream = file.OpenReadStream())
            {
                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(BucketName)
                    .WithObject(objectNameAndId)
                    .WithStreamData(stream)
                    .WithObjectSize(file.Length)
                    .WithContentType(file.ContentType);

                await _minioClient.PutObjectAsync(putObjectArgs);
            }

            var newFileRecord = new FileRecord(
                fileId: objectNameAndId,
                fileName: objectNameAndId,
                fileLocation: $"{BucketName}/{objectNameAndId}",
                fileCreatedTime: DateTime.UtcNow,
                fileSlides: await ExtractSlidesFromFile(file, objectNameAndId)
            );
            _FilesRecords.Add(newFileRecord);

            return CreatedAtAction(nameof(GetFileRecordById), new { id = newFileRecord.FileId }, new { fileRecord = newFileRecord });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error uploading file: {ex.Message}");
        }
    }

    private async Task<List<SlideRecord>> ExtractSlidesFromFile(IFormFile file, string fileId)
    {
        var slides = new List<SlideRecord>();

        // Ensure slides bucket exists
        var bucketExistsArgs = new BucketExistsArgs()
            .WithBucket(SlidesBucketName);
        bool bucketExists = await _minioClient.BucketExistsAsync(bucketExistsArgs);

        if (!bucketExists)
        {
            var makeBucketArgs = new MakeBucketArgs()
                .WithBucket(SlidesBucketName);
            await _minioClient.MakeBucketAsync(makeBucketArgs);
        }

        // Create temporary directory for processing
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            using (var stream = file.OpenReadStream())
            {
                var sourceDocument = PdfReader.Open(stream, PdfDocumentOpenMode.Import);

                for (int pageNumber = 0; pageNumber < sourceDocument.PageCount; pageNumber++)
                {
                    // Create a new document for each page
                    var slideDocument = new PdfSharpCore.Pdf.PdfDocument();
                    slideDocument.AddPage(sourceDocument.Pages[pageNumber]);

                    // Generate unique ID for the slide
                    string slideId = GenerateID.GenerateRandomId();
                    string slideObjectName = $"{fileId}/slide_{pageNumber + 1}_{slideId}.png";

                    // Save the single page PDF temporarily
                    string tempPdfPath = Path.Combine(tempDir, $"temp_slide_{pageNumber + 1}.pdf");
                    using (var fs = new FileStream(tempPdfPath, FileMode.Create))
                    {
                        slideDocument.Save(fs);
                    }

                    // Convert PDF to PNG using Python script
                    string tempPngPath = Path.Combine(tempDir, $"temp_slide_{pageNumber + 1}.png");
                    var processStartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "python3",
                        Arguments = $"PDF2Image.py \"{tempPdfPath}\" \"{tempPngPath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (var process = System.Diagnostics.Process.Start(processStartInfo))
                    {
                        await process.WaitForExitAsync();
                        if (process.ExitCode != 0)
                        {
                            throw new Exception($"PDF to image conversion failed: {await process.StandardError.ReadToEndAsync()}");
                        }
                    }

                    // Upload PNG to MinIO
                    using (var imageStream = new FileStream(tempPngPath, FileMode.Open))
                    {
                        var putObjectArgs = new PutObjectArgs()
                            .WithBucket(SlidesBucketName)
                            .WithObject(slideObjectName)
                            .WithStreamData(imageStream)
                            .WithObjectSize(imageStream.Length)
                            .WithContentType("image/png");

                        await _minioClient.PutObjectAsync(putObjectArgs);
                    }

                    // Add slide record
                    slides.Add(new SlideRecord(
                        slideId: slideId,
                        slideNumber: pageNumber + 1,
                        slideLocation: $"{SlidesBucketName}/{slideObjectName}"
                    ));

                    // Clean up temporary files
                    System.IO.File.Delete(tempPdfPath);
                    System.IO.File.Delete(tempPngPath);
                }
            }
        }
        finally
        {
            // Clean up temporary directory
            Directory.Delete(tempDir, true);
        }

        return slides;
    }

    [HttpGet("{fileId}/slides/{slideNumber}")]
    public async Task<IActionResult> GetSlideByNumber(string fileId, int slideNumber)
    {
        var fileRecord = _FilesRecords.FirstOrDefault(fr => fr.FileId == fileId);
        if (fileRecord == null)
            return NotFound("File record not found");

        var slide = fileRecord.FileSlides.FirstOrDefault(s => s.SlideNumber == slideNumber);
        if (slide == null)
            return NotFound($"Slide number {slideNumber} not found");

        try
        {
            using var memoryStream = new MemoryStream();

            await _minioClient.GetObjectAsync(new GetObjectArgs()
                .WithBucket(SlidesBucketName)
                .WithObject($"{fileId}/slide_{slideNumber}_{slide.SlideId}.png")
                .WithCallbackStream(stream => stream.CopyTo(memoryStream)));

            memoryStream.Position = 0;
            return File(memoryStream.ToArray(), "image/png");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving slide: {ex.Message}");
        }
    }

    private int GetPdfPageCount(IFormFile file)
    {
        using (var stream = file.OpenReadStream())
        {
            var document = PdfReader.Open(stream, PdfDocumentOpenMode.ReadOnly);
            return document.PageCount;
        }
    }
}