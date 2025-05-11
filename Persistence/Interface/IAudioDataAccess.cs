public interface IAudioDataAccess
{
    public AudioRecord AddAudioRecord(AudioRecord audioRecord);
    public AudioRecord GetAudioRecord(string audioRecordId, string sessionId);
}