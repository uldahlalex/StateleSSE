namespace dataaccess;

public partial class Game
{
    public string Id { get; set; } = null!;

    public string Hostid { get; set; } = null!;

    public string Quizid { get; set; } = null!;

    public virtual ICollection<Gamemember> Gamemembers { get; set; } = new List<Gamemember>();

    public virtual ICollection<Gameround> Gamerounds { get; set; } = new List<Gameround>();

    public virtual User Host { get; set; } = null!;

    public virtual Quiz Quiz { get; set; } = null!;
}