namespace dataaccess;

public partial class WeatherStation
{
    public string Id { get; set; } = null!;

    public string Name { get; set; } = null!;

    public virtual ICollection<WeatherReading> WeatherReadings { get; set; } = new List<WeatherReading>();
}
