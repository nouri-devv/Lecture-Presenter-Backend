using Microsoft.AspNetCore.Mvc;
using PdfSharpCore.Pdf.IO;

[ApiController]
[Route("api/file-record")]
public class FileRecordController : ControllerBase
{
    private static readonly List<FileRecord> _FilesRecords = new List<FileRecord>();
    // Removed duplicate slide records list

    [HttpGet("{id}", Name = "GetFileRecordById")]
    public IActionResult GetFileRecordById(string id)
    {
        var fileRecord = _FilesRecords.FirstOrDefault(fr => fr.Id == id);
        if (fileRecord == null)
            return NotFound();
        
        return Ok(fileRecord);
    }

    [HttpPost]
    public IActionResult CreateFileRecord(IFormFile file)
    {
        if (file == null)
            return BadRequest("File record cannot be null.");

        var newFileRecord = new FileRecord(
            id: GenerateID.GenerateRandomId(),
            name: file.FileName,
            fileLocation: file.FileName, // In a real implementation, save the file and provide the path
            createdTime: DateTime.UtcNow,
            slides: ExtractSlidesFromFile(file)
        );
        _FilesRecords.Add(newFileRecord);

        
        return CreatedAtAction(nameof(GetFileRecordById), new { id = newFileRecord.Id }, new { fileRecord = newFileRecord });
    }

    private List<SlideRecord> ExtractSlidesFromFile(IFormFile file)
    {
        var slides = new List<SlideRecord>();
        int pageCount = GetPdfPageCount(file);
        for (int i = 1; i <= pageCount; i++)
            slides.Add(new SlideRecord(slideId: GenerateID.GenerateRandomId(), slideNumber: i));
        
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