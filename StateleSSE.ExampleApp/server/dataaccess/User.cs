namespace dataaccess;

public partial class User
{
    public string Id { get; set; } = null!;

    public string Name { get; set; } = null!;

    public virtual ICollection<Answer> Answers { get; set; } = new List<Answer>();

    public virtual ICollection<Gamemember> Gamemembers { get; set; } = new List<Gamemember>();

    public virtual ICollection<Game> Games { get; set; } = new List<Game>();

    public virtual ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();

    public virtual Userscredential? Userscredential { get; set; }
}