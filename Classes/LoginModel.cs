public class LoginModel
{
    public string Email { get; set; }
    public string PasswordHash { get; set; }

    public LoginModel(string email, string passwordHash)
    {
        Email = email;
        PasswordHash = passwordHash;
    }
}