public interface IUserDataAccess
{
    User CreateUser(User user);
    User GetUserByEmail(string email);
}