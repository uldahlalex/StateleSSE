namespace dataaccess;

public partial class Quiz
{
    public string Id { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Createdby { get; set; } = null!;

    public virtual User CreatedbyNavigation { get; set; } = null!;

    public virtual ICollection<Game> Games { get; set; } = new List<Game>();

    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
}