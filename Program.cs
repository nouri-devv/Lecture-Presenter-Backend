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

var app = builder.Build();

app.MapControllers();
app.MapGet("/", () => "Hello World!");
app.Run();