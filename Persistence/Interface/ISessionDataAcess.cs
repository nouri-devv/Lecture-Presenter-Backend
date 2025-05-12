public interface ISessionDataAccess
{
    Session AddSession(string sessionId, DateTime createdDate, DateTime lastModifiedDate);
}