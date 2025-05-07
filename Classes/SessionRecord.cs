public class SessionRecord
{
    public string SessionId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime LastModifiedDate { get; set; }
    public FileRecord SessionFile { get; set; }

    public SessionRecord(string sessionId, DateTime createdDate, DateTime lastModifiedDate, FileRecord sessionFile)
    {
        SessionId = sessionId;
        CreatedDate = createdDate;
        LastModifiedDate = lastModifiedDate;
        SessionFile = sessionFile;
    }
}