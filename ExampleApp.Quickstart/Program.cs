using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApiDocument();
builder.Services.AddEfRealtime();
builder.Services.AddDbContext<AppDb>((sp, opt) =>
{
    opt.UseInMemoryDatabase("quickstart");
    opt.AddEfRealtimeInterceptor(sp);
});
builder.Services.AddControllers();
var app = builder.Build();
app.MapControllers();
app.UseOpenApi();
app.UseSwaggerUi();

app.Run();
