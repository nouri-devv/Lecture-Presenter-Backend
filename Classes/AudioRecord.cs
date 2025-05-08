public class AudioRecord
{
    public string AudioRecordId { get; set; }
    public int AudioRecordNumber { get; set; }
    public string AudioRecordLocation { get; set; }

    public AudioRecord(string audioRecordId, string audioRecordLocation)
    {
        AudioRecordId = audioRecordId;
        AudioRecordLocation = audioRecordLocation;
    }
}