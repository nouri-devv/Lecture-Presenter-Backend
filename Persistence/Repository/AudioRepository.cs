using Npgsql;
using PdfSharpCore.Pdf.Content.Objects;

public class AudioRespository : IRepository, IAudioDataAccess
{
    private IRepository _repository => this;

    public AudioRecord AddAudioRecord(AudioRecord audioRecord, string sessionId)
    {
        var sqlParameters = new NpgsqlParameter[]
        {
            new("audio_id", audioRecord.AudioRecordId),
            new("audio_number", audioRecord.AudioRecordNumber),
            new("audio_location", audioRecord.AudioRecordLocation),
            new("session_id", sessionId)
        };

        var result = _repository.ExecuteReader<AudioRecord>(
            "INSERT INTO audio (audio_id, audio_number, audio_location, session_id) " +
            "VALUES (@audio_id, @audio_number, @audio_location, @session_id) " +
            "RETURNING *", sqlParameters).Single();

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
            sqlParameters).Single();

        return result;
    }
}