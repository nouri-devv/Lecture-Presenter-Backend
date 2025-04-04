var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

var app = builder.Build();

// Map controllers
app.MapControllers();


app.Run();app.Run();