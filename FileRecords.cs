public class FileRecords 
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string FileLocation { get; set; }
    public DateTime CreatedTime { get; set; }

    public FileRecords(string id, string name, string fileLocation, DateTime createdTime)
    {
        Id = id;
        Name = name;
        FileLocation = fileLocation;
        CreatedTime = createdTime;
    }
}