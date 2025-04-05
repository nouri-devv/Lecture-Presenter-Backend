using Microsoft.AspNetCore.Mvc;

[Route("api/file-upload")]
[ApiController]
public class FileUploadController : ControllerBase
{
    private readonly string _uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "UploadedFiles");

    [HttpPost]
    [RequestSizeLimit(512L * 1024 * 1024)] // 0.5GB
    public IActionResult UploadFile([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        // Validate file type
        var allowedExtensions = new[] { ".pdf" };
        var fileExtension = Path.GetExtension(file.FileName).ToLower();
        if (!allowedExtensions.Contains(fileExtension)) 
            return BadRequest("Invalid file type.");

        try
        {
            // Ensure the upload directory exists
            if (!Directory.Exists(_uploadPath))
                Directory.CreateDirectory(_uploadPath);

            // Save the file to the upload directory
            var sessionId = Path.GetRandomFileName();
            var filePath = Path.Combine(_uploadPath, sessionId + fileExtension);
            using (var stream = new FileStream(filePath, FileMode.Create))
                file.CopyTo(stream);

            return Ok(new { fileName = file.FileName, FileSize = file.Length, savedPath = filePath, sessionId });
        }
        catch
        {
            return StatusCode(500, "An error occurred while uploading the file.");
        }
    }
}