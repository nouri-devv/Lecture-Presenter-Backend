using Npgsql;
using PdfSharpCore.Pdf.Content.Objects;

public class AudioRespository : IRepository, IAudioDataAccess
{
    private IRepository _repository => this;

    public AudioRecord AddAudioRecord(AudioRecord audioRecord)
    {
        var sqlParameters = new NpgsqlParameter[]
        {
            new("audio_id", audioRecord.AudioRecordId),
            new("session_id", audioRecord.SessionId),
            new("audio_number", audioRecord.AudioRecordNumber),
            new("audio_location", audioRecord.AudioRecordLocation)
        };

        var result = _repository.ExecuteReader<AudioRecord>(
            "INSERT INTO audio (audio_id, session_id, audio_number, audio_location) " +
            "VALUES (@audio_id, @session_id, @audio_number, @audio_location) " +
            "RETURNING audio_id, session_id, audio_number, audio_location",
            sqlParameters).SingleOrDefault();

        return result;
    }
    public AudioRecord GetAudioRecord(string audioRecordId, string sessionId)
    {
        var sqlParameters = new NpgsqlParameter[]
        {
            new("audio_id", audioRecordId),
            new("session_id", sessionId)
        };
        var result = _repository.ExecuteReader<AudioRecord>(
            "SELECT * FROM audio WHERE audio_id = @audio_id AND session_id = @session_id",
            sqlParameters).SingleOrDefault();

        return result;
    }
}