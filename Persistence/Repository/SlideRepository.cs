using Npgsql;

public class SlideRepository : ISlideDataAccess, IRepository
{
    private IRepository _repository => this;

    public Slide CreateSlide(Slide slide)
    {
        {
            var sqlParam = new NpgsqlParameter[]
            {
            new("session_id", slide.SessionId),
            new("slide_number", slide.SlideNumber),
            new("slide_location", slide.SlideLocation)
            };

            _repository.ExecuteReader<Slide>(
                "INSERT INTO slides (session_id, slide_number, slide_location) " +
                "VALUES (@session_id, @slide_number, @slide_location) ",
                sqlParam).SingleOrDefault();

            return slide;
        }
    }
}