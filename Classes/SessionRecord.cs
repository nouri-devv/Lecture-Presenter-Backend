public class SessionRecord
{
    public string SessionId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime LastModifiedDate { get; set; }
    public List<SlideRecord> SlideRecords { get; set; } = new List<SlideRecord>();
    public List<AudioRecord> AudioRecords { get; set; } = new List<AudioRecord>();
    public List<LlmResponseRecord> LlmResponses { get; set; } = new List<LlmResponseRecord>();

    public SessionRecord(string sessionId, DateTime createdDate, DateTime lastModifiedDate, List<SlideRecord> slideRecords)
    {
        SessionId = sessionId;
        CreatedDate = createdDate;
        LastModifiedDate = lastModifiedDate;
        SlideRecords = slideRecords;
    }
}