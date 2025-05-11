public interface IAudioDataAccess
{
    public AudioRecord AddAudioRecord(AudioRecord audioRecord, string sessionId);
    public AudioRecord GetAudioRecord(string audioRecordId, string sessionId);
}