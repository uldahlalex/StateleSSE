using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.EfRealtime;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApiDocument();
builder.Services.AddSingleton<RealtimeManager>();
builder.Services.AddSingleton<IRealtimeManager>((sp) => sp.GetRequiredService<RealtimeManager>());
builder.Services.AddSingleton<RealtimeSaveChangesInterceptor>();
builder.Services.AddDbContext<AppDb>((sp, opt) =>
{
    opt.UseInMemoryDatabase("quickstart");
    opt.AddInterceptors(sp.GetRequiredService<RealtimeSaveChangesInterceptor>());
});
builder.Services.AddControllers();
var app = builder.Build();
app.MapControllers();
app.UseOpenApi();
app.UseSwaggerUi();

app.Run();
