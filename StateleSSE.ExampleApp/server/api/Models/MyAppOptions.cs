using System.ComponentModel.DataAnnotations;

namespace api.Models;

public sealed class MyAppOptions
{
    [Required]
    [MinLength(20)]
    public string Db { get; set; } = "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=postgres";

    [Required] [MinLength(5)] public string JwtSecret { get; set; }
    public string? Redis { get; set; }
    [Required] [MinLength(5)] public string MQTT_BROKER { get; set; }
    public string MQTT_USERNAME { get; set; }
    public string MQTT_PASS { get; set; }
}