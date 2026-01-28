var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromMemory(
        routes:
        [
            new Yarp.ReverseProxy.Configuration.RouteConfig
            {
                RouteId = "all",
                ClusterId = "servers",
                Match = new Yarp.ReverseProxy.Configuration.RouteMatch { Path = "{**catch-all}" }
            }
        ],
        clusters:
        [
            new Yarp.ReverseProxy.Configuration.ClusterConfig
            {
                ClusterId = "servers",
                LoadBalancingPolicy = "RoundRobin",
                Destinations = new Dictionary<string, Yarp.ReverseProxy.Configuration.DestinationConfig>
                {
                    ["server1"] = new() { Address = "http://localhost:5001" },
                    ["server2"] = new() { Address = "http://localhost:5002" }
                }
            }
        ]);

var app = builder.Build();
app.MapReverseProxy();
app.Run();
