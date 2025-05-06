public class FileRecord
{
    public string FileId { get; set; }
    public string FileName { get; set; }
    public string FileLocation { get; set; }
    public DateTime FileCreatedTime { get; set; }
    public List<SlideRecord> FileSlides { get; set; } = new List<SlideRecord>();

    public FileRecord(string fileId, string fileName, string fileLocation, DateTime fileCreatedTime, List<SlideRecord> fileSlides)
    {
        FileId = fileId;
        FileName = fileName;
        FileLocation = fileLocation;
        FileCreatedTime = fileCreatedTime;
        FileSlides = fileSlides;
    }
}