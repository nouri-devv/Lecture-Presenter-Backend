using Npgsql;

public class UserRespository : IUserDataAccess, IRepository
{
    private IRepository _repository => this;

    public User CreateUser(User user)
    {
        var sqplParam = new NpgsqlParameter[]{
            new("user_id", user.UserId),
            new("user_first_name", user.UserFirstName),
            new("user_last_name", user.UserLastName),
            new("user_email", user.UserEmail),
            new("user_hash_password", user.UserHashPassword)
        };

        _repository.ExecuteReader<User>(
            "INSERT INTO users (user_id, user_first_name, user_last_name, user_email, user_hash_password) " +
            "VALUES (@user_id, @user_first_name, @user_last_name, @user_email, @user_hash_password) ",
            sqplParam).SingleOrDefault();

        return user;
    }

    public User GetUserByEmail(string email)
    {
        var sqlParam = new NpgsqlParameter[]{
            new("user_email", email)
        };

        var result = _repository.ExecuteReader<User>(
            "SELECT " +
            "user_id AS \"UserId\", " +
            "user_first_name AS \"UserFirstName\", " +
            "user_last_name AS \"UserLastName\", " +
            "user_email AS \"UserEmail\", " +
            "user_hash_password AS \"UserHashPassword\" " +
            "FROM users WHERE LOWER(user_email) = LOWER(@user_email)",
            sqlParam).SingleOrDefault();

        if (result == null)
        {
            Console.WriteLine($"No user found with email: {email}");
        }

        return result;
    }
}