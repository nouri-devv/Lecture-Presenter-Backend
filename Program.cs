using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Minio.AspNetCore;
using DotNetEnv;
using System.Text;
Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddControllers();
builder.Services.AddScoped<IUserDataAccess, UserRespository>();
builder.Services.AddScoped<ISessionDataAccess, SessionRepository>();
builder.Services.AddScoped<IAudioDataAccess, AudioRespository>();
builder.Services.AddScoped<ISlideDataAccess, SlideRepository>();
builder.Services.AddScoped<LlmResponseDataAccess, LlmResponseRepository>();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = "yourapp", // Replace with your actual issuer

        ValidateAudience = true,
        ValidAudience = "yourapp", // Replace with your actual audience

        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? throw new InvalidOperationException("JWT_SECRET_KEY is not configured.")
        )),

        ValidateLifetime = true, // Ensure the token hasn't expired
        ClockSkew = TimeSpan.Zero // Optional: Adjust for clock skew if needed
    };
});
builder.Services.AddMinio(options =>
{
    options.Endpoint = Environment.GetEnvironmentVariable("MINIO_ENDPOINT");
    options.AccessKey = Environment.GetEnvironmentVariable("MINIO_ACCESS_KEY");
    options.SecretKey = Environment.GetEnvironmentVariable("MINIO_SECRET_KEY");
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNextJS",
        policy =>
        {
            policy.WithOrigins("http://localhost:3000")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
});


var app = builder.Build();

app.UseCors("AllowNextJS");
app.MapControllers();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/", () => "Hello World!");
app.Run();