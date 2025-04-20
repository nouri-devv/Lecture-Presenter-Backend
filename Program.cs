var builder = WebApplication.CreateBuilder(args);

// Register IHttpClientFactory
builder.Services.AddHttpClient();

// Add this to enable attribute-based controllers
builder.Services.AddControllers();

var app = builder.Build();

// route controller actions
app.MapControllers();

app.MapGet("/", () => "Hello World!");

app.Run();