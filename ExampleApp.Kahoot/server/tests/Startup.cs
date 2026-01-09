using api;
using dataaccess;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using mqtt;
using NSubstitute;
using Testcontainers.PostgreSql;

namespace tests;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        Program.ConfigureServices(services);

       
        services.RemoveAll(typeof(MyDbContext));
        services.AddScoped<PostgreSqlContainer>(sp =>
        {
            var container = new PostgreSqlBuilder().Build();
            container.StartAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            return container;
        });

        services.AddScoped<MyDbContext>(serviceProvider =>
        {
            var postgreSqlContainer = serviceProvider.GetRequiredService<PostgreSqlContainer>();
            var connectionString = postgreSqlContainer.GetConnectionString();

            var optionsBuilder = new DbContextOptionsBuilder<MyDbContext>();
            optionsBuilder.UseNpgsql(connectionString);


            return new MyDbContext(optionsBuilder.Options);
        });


        // Register mocks as SCOPED for test isolation (fresh instances per test)
        services.RemoveAll(typeof(IMqttService));

        // Mock typed IHubContext<QuizHub, IQuizBroadcasts>

        services.AddScoped<IMqttService>(sp =>
        {
            var mockMqtt = Substitute.For<IMqttService>();
            mockMqtt.IsConnected.Returns(true);
            return mockMqtt;
        });
        
        //todo replace backplane
   }
}