using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEfRealtime();
builder.Services.AddDbContext<AppDb>((sp, opt) =>
{
    opt.UseInMemoryDatabase("quickstart");
    opt.AddEfRealtimeInterceptor(sp);
});
builder.Services.AddControllers();
var app = builder.Build();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var ctx = scope.ServiceProvider.GetRequiredService<AppDb>();
    
}

app.Run();
