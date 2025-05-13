using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using System.Security.Claims;


public class BearerAuthenticationHandler : JwtBearerHandler
{
    private readonly IUserDataAccess _usersRepo;

    public BearerAuthenticationHandler(
        IOptionsMonitor<JwtBearerOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IUserDataAccess usersRepo)
        : base(options, logger, encoder, clock)
    {
        _usersRepo = usersRepo;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var result = await base.HandleAuthenticateAsync();

        if (!result.Succeeded)
            return result;

        var principal = result.Principal;
        var email = principal?.FindFirst(ClaimTypes.Email)?.Value;

        if (string.IsNullOrWhiteSpace(email))
            return AuthenticateResult.Fail("Email claim missing in JWT.");

        var user = _usersRepo.GetUserByEmail(email);
        if (user == null)
            return AuthenticateResult.Fail("User not found in DB.");

        var claims = new List<Claim>(principal.Claims)
        {
            new Claim(ClaimTypes.Email, user.UserEmail),
            new Claim(ClaimTypes.Name, $"{user.UserFirstName} {user.UserLastName}"),
            new Claim("LastLoginDate", DateTime.UtcNow.ToString("o"))
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var updatedPrincipal = new ClaimsPrincipal(identity);

        return AuthenticateResult.Success(new AuthenticationTicket(updatedPrincipal, Scheme.Name));
    }
}
