using Npgsql;

public class SlideRepository : ISlideDataAccess, IRepository
{
    private IRepository _repository => this;

    public SlideRecord CreateSlide(SlideRecord slideRecord, string sessionId)
    {
        {
            var sqlParam = new NpgsqlParameter[]
            {
            new("slide_id", slideRecord.SlideId),
            new("slide_number", slideRecord.SlideNumber),
            new("slide_location", slideRecord.SlideLocation),
            new("session_id", sessionId)
            };

            var result = _repository.ExecuteReader<SlideRecord>(
                "INSERT INTO slides (slide_id, slide_number, slide_location, session_id) " +
                "VALUES (@slide_id, @slide_number, @slide_location, @session_id) " +
                "RETURNING slide_id, slide_number, slide_location, session_id",
                sqlParam).Single();

            return result;
        }
    }

    public SlideRecord GetSlide(string sessionId, int slideNumber)
    {
        var sqlParam = new NpgsqlParameter[] {
            new("session_id", sessionId),
            new("slide_number", slideNumber)
        };

        var result = _repository.ExecuteReader<SlideRecord>(
            "SELECT slide_id, slide_number, slide_location " +
            "FROM slides WHERE session_id = @session_id AND slide_number = @slide_number",
            sqlParam).Single();

        return result;
    }
}
