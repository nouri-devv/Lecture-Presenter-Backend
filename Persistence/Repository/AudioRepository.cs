using Npgsql;
using PdfSharpCore.Pdf.Content.Objects;

public class AudioRespository : IRepository, IAudioDataAccess
{
    private IRepository _repository => this;

    public Audio AddAudio(Audio audio)
    {
        var sqlParameters = new NpgsqlParameter[]
        {
            new("session_id", audio.SessionId),
            new("audio_number", audio.AudioNumber),
            new("audio_location", audio.AudioLocation)
        };

        var result = _repository.ExecuteReader<Audio>(
            "INSERT INTO audios (session_id, audio_number, audio_location) " +
            "VALUES (@session_id, @audio_number, @audio_location) ",
            sqlParameters).SingleOrDefault();

        return audio;
    }
}