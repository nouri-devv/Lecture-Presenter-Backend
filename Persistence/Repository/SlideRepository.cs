using Npgsql;

public class SlideRepository : ISlideDataAccess, IRepository
{
    private IRepository _repository => this;

    public SlideRecord CreateSlide(SlideRecord slideRecord)
    {
        {
            var sqlParam = new NpgsqlParameter[]
            {
            new("session_id", slideRecord.SessionId),
            new("slide_number", slideRecord.SlideNumber),
            new("slide_location", slideRecord.SlideLocation)
            };

            var result = _repository.ExecuteReader<SlideRecord>(
                "INSERT INTO slides (session_id, slide_number, slide_location) " +
                "VALUES (@session_id, @slide_number, @slide_location) " +
                "RETURNING session_id, slide_number, slide_location",
                sqlParam).SingleOrDefault();

            return result;
        }
    }
}