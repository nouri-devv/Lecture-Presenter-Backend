public class FileRecord
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string FileLocation { get; set; }
    public DateTime CreatedTime { get; set; }
    public List<SlideRecord> Slides { get; set; } = new List<SlideRecord>();

    public FileRecord(string id, string name, string fileLocation, DateTime createdTime, List<SlideRecord> slides)
    {
        Id = id;
        Name = name;
        FileLocation = fileLocation;
        CreatedTime = createdTime;
        Slides = slides;
    }
}