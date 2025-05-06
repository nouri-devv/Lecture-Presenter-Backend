using Minio.AspNetCore;
using DotNetEnv;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddControllers();
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

app.MapControllers();
app.UseCors("AllowNextJS");
app.MapGet("/", () => "Hello World!");
app.Run();