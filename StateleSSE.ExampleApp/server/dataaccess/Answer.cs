namespace dataaccess;

public partial class Answer
{
    public string Userid { get; set; } = null!;

    public string Gameround { get; set; } = null!;

    public string Option { get; set; } = null!;

    public DateTime? Answeredat { get; set; }

    public virtual Gameround GameroundNavigation { get; set; } = null!;

    public virtual Option OptionNavigation { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}