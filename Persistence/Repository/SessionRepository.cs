using Npgsql;

public class SessionRepository : ISessionDataAccess, IRepository
{
    private IRepository _repository => this;

    public SessionRecord AddSession(string sessionId, DateTime createdDate, DateTime lastModifiedDate)
    {
        var sqlParam = new NpgsqlParameter[]
        {
            new("session_id", sessionId),
            new("created_date", createdDate),
            new("last_modified_date", lastModifiedDate),
        };

        var result = _repository.ExecuteReader<SessionRecord>(
            "INSERT INTO sessions (session_id, created_date, last_modified_date) " +
            "VALUES (@session_id, @created_date, @last_modified_date) " +
            "RETURNING session_id, created_date, last_modified_date",
            sqlParam).Single();

        return result;
    }
}