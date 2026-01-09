namespace dataaccess;

public partial class Gamemember
{
    public string Userid { get; set; } = null!;

    public string Gameid { get; set; } = null!;

    public DateTime Joinedat { get; set; }

    public virtual Game Game { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}