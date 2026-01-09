namespace dataaccess;

public partial class Option
{
    public string Id { get; set; } = null!;

    public string Description { get; set; } = null!;

    public bool Iscorrect { get; set; }

    public string Questionid { get; set; } = null!;

    public virtual ICollection<Answer> Answers { get; set; } = new List<Answer>();

    public virtual Question Question { get; set; } = null!;
}