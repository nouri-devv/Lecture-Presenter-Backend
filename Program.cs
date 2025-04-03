var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

const string filePath = "data.txt";
const string filePath2 = "data2.txt";
const string filePath3 = "data3.txt";
const string filePath4 = "data4.txt";
const string filePath5 = "data5.txt";

app.MapGet("/", () => "Hello World!");

app.Run();