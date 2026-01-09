namespace dataaccess;

public partial class Gameround
{
    public string Id { get; set; } = null!;

    public string Questionid { get; set; } = null!;

    public string Gameid { get; set; } = null!;

    public DateTime Startedat { get; set; }

    public DateTime? Endedat { get; set; }

    public virtual ICollection<Answer> Answers { get; set; } = new List<Answer>();

    public virtual Game Game { get; set; } = null!;

    public virtual Question Question { get; set; } = null!;
}