public class FileRecords 
{
    public string Id { get; set; }
    public string FileLocation { get; set; }
    public DateTime CreatedTime { get; set; }

    public FileRecords(string id, string fileLocation, DateTime createdTime)
    {
        Id = id;
        FileLocation = fileLocation;
        CreatedTime = createdTime;
    }
}