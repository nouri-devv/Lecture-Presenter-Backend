using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;


[ApiController]
[Authorize]
[Route("api/user")]
public class UserController : ControllerBase
{
    private readonly IUserDataAccess _userDataAccess;

    public UserController(IUserDataAccess userDataAccess)
    {
        _userDataAccess = userDataAccess;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(body))
            return BadRequest("Request body is empty.");

        // Deserialize JSON manually
        var userData = JsonSerializer.Deserialize<Dictionary<string, string>>(body);

        if (userData == null ||
            !userData.TryGetValue("UserFirstName", out var firstName) ||
            !userData.TryGetValue("UserLastName", out var lastName) ||
            !userData.TryGetValue("UserEmail", out var email) ||
            !userData.TryGetValue("UserHashPassword", out var password))
        {
            return BadRequest("Missing required fields.");
        }

        // Check if user already exists
        var existingUser = _userDataAccess.GetUserByEmail(email);
        if (existingUser != null)
            return Conflict("Email already exists");

        // Create new user
        var user = new User
        {
            UserId = Guid.NewGuid().ToString(),
            UserFirstName = firstName,
            UserLastName = lastName,
            UserEmail = email,
            UserHashPassword = BCrypt.Net.BCrypt.HashPassword(password)
        };

        var createdUser = _userDataAccess.CreateUser(user);
        if (createdUser == null)
            return StatusCode(500, "Error creating user");

        return CreatedAtAction(nameof(CreateUser), new
        {
            UserId = createdUser.UserId,
            UserEmail = createdUser.UserEmail,
            UserFirstName = createdUser.UserFirstName,
            UserLastName = createdUser.UserLastName
        });
    }

    [HttpGet]
    public IActionResult GetUserByEmail(string email)
    {
        if (string.IsNullOrEmpty(email))
            return BadRequest("Email cannot be null or empty");

        var user = _userDataAccess.GetUserByEmail(email);
        if (user == null)
            return NotFound("User not found");

        return Ok(user);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(body))
            return BadRequest("Request body is empty.");

        var loginData = JsonSerializer.Deserialize<Dictionary<string, string>>(body);

        if (loginData == null ||
            !loginData.TryGetValue("email", out var email) ||
            !loginData.TryGetValue("password", out var password))
        {
            return BadRequest("Email or password is missing.");
        }

        var user = _userDataAccess.GetUserByEmail(email);
        if (user == null || string.IsNullOrEmpty(user.UserHashPassword))
            return Unauthorized("Invalid email or password.");

        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, user.UserHashPassword);
        if (!isPasswordValid)
            return Unauthorized("Invalid email or password.");

        // Create claims for the JWT
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId),
            new Claim(ClaimTypes.Email, user.UserEmail),
            new Claim(ClaimTypes.Name, $"{user.UserFirstName} {user.UserLastName}"),
            new Claim("LastLoginDate", DateTime.UtcNow.ToString("o"))
        };

        // Use a secure method to retrieve the secret key
        var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
        if (string.IsNullOrEmpty(secretKey))
            return StatusCode(500, "JWT secret key is not configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "yourapp",
            audience: "yourapp",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new
        {
            token = tokenString,
            user = new
            {
                user.UserId,
                user.UserEmail,
                user.UserFirstName,
                user.UserLastName,
            }
        });
    }
}