namespace dataaccess;

public partial class WeatherReading
{
    public string Id { get; set; } = null!;

    public string Stationid { get; set; } = null!;

    public decimal Temperature { get; set; }

    public decimal Humidity { get; set; }

    public decimal Pressure { get; set; }

    public DateTime Timestamp { get; set; }

    public virtual WeatherStation Station { get; set; } = null!;
}
