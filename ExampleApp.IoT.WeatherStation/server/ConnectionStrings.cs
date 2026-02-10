namespace server;

public class ConnectionStrings
{
    public string DbConnectionString { get; set; }
    public string MqttBroker { get; set; }    
    public int MqttPort { get; set; }    
    // public string MqttUsername { get; set; }
    //                                          
    // public string MqttPassword { get; set; }
    public string Redis { get; set; }

    public string Secret { get; set; }
}