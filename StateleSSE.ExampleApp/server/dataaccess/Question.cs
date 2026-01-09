namespace dataaccess;

public partial class Question
{
    public string Id { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string Quizid { get; set; } = null!;

    public int Seconds { get; set; }

    public virtual ICollection<Gameround> Gamerounds { get; set; } = new List<Gameround>();

    public virtual ICollection<Option> Options { get; set; } = new List<Option>();

    public virtual Quiz Quiz { get; set; } = null!;
}