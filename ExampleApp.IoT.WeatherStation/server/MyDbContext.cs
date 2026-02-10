using Microsoft.EntityFrameworkCore;

namespace server;

public class MyDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)                                                                                           
    {                                                                                                                                                            
        modelBuilder.HasDefaultSchema("iot_weatherstation");                                                                                          
    }      
    
    public MyDbContext(DbContextOptions<MyDbContext> options)
        : base(options)
    {
        
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Station> Stations { get; set; }
    public DbSet<Measurement> Measurements { get; set; }
}


[PrimaryKey(nameof(Id))]
public class User
{
    public string Id { get; set; }
    public string Nickname { get; set; }
    public string Salt { get; set; }
    public string Hash { get; set; }
}

[PrimaryKey(nameof(Id))]
public class Station
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string CreatedBy { get; set; }
}

[PrimaryKey(nameof(Id))]
public class Measurement
{
    public Guid Id { get; set; }
    public string StationId { get; set; }
    public string SensorId { get; set; }
    public DateTime Timestamp { get; set; }
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public double Pressure { get; set; }
    public int LightLevel { get; set; }
}

