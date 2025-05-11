public interface ISessionDataAccess
{
    SessionRecord AddSession(string sessionId, DateTime createdDate, DateTime lastModifiedDate);
}