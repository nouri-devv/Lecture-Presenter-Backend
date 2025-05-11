public interface ISessionDataAccess
{
    SessionRecord CreateSession(string sessionId, DateTime createdDate, DateTime lastModifiedDate);
}