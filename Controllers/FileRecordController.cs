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
                Console.WriteLine($"Uploading file with content type: {file.ContentType}");
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
                string slideObjectName = $"{fileId}/slide_{pageNumber + 1}_{slideId}";

                // Save the single page to a memory stream
                using (var memoryStream = new MemoryStream())
                {
                    slideDocument.Save(memoryStream);
                    memoryStream.Position = 0;

                    // Upload to MinIO
                    var putObjectArgs = new PutObjectArgs()
                        .WithBucket(SlidesBucketName)
                        .WithObject(slideObjectName)
                        .WithStreamData(memoryStream)
                        .WithObjectSize(memoryStream.Length)
                        .WithContentType("application/pdf");

                    await _minioClient.PutObjectAsync(putObjectArgs);
                }

                // Add slide record
                slides.Add(new SlideRecord(
                    slideId: slideId,
                    slideNumber: pageNumber + 1,
                    slideLocation: $"{SlidesBucketName}/{slideObjectName}"
                ));
            }
        }

        return slides;
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