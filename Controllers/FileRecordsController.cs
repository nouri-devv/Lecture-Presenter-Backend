using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/file-records")]
public class FileRecordsController : ControllerBase
{
    private static readonly List<FileRecords> _FilesRecords = new List<FileRecords>();

    [HttpGet("{id}", Name = "GetFileRecordById")]
    public IActionResult GetFileRecordById([FromBody] string id)
    {
        var fileRecord = _FilesRecords.FirstOrDefault(fr => fr.Id == id);
        if (fileRecord == null)
            return NotFound();
        
        return Ok(fileRecord);
    }

    [HttpPost]
    public IActionResult CreateFileRecord([FromBody] FileRecords fileRecord)
    {
        if (fileRecord == null)
            return BadRequest("File record cannot be null.");

        fileRecord.Id = GenerateID.GenerateRandomId();
    
        // FIX THIS: need a proper way to add the file to a locations
        fileRecord.FileLocation = fileRecord.FileLocation;
        fileRecord.CreatedTime = DateTime.UtcNow;
        _FilesRecords.Add(fileRecord);
        
        return CreatedAtAction(nameof(GetFileRecordById), new { id = fileRecord.Id }, fileRecord);
    }
}