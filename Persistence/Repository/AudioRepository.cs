using Npgsql;
using PdfSharpCore.Pdf.Content.Objects;

public class AudioRespository : IRepository, IAudioDataAccess
{
    private IRepository _repository => this;

    public AudioRecord AddAudioRecord(AudioRecord audioRecord)
    {
        var sqlParameters = new NpgsqlParameter[]
        {
            new("session_id", audioRecord.SessionId),
            new("audio_number", audioRecord.AudioRecordNumber),
            new("audio_location", audioRecord.AudioRecordLocation)
        };

        var result = _repository.ExecuteReader<AudioRecord>(
            "INSERT INTO audios (session_id, audio_number, audio_location) " +
            "VALUES (@session_id, @audio_number, @audio_location) " +
            "RETURNING session_id, audio_number, audio_location",
            sqlParameters).SingleOrDefault();

        return result;
    }
}