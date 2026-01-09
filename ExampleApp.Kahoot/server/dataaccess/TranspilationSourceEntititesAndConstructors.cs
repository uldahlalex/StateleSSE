namespace dataaccess;

public partial class Userscredential
{
    private Userscredential()
    {
    }

    public Userscredential(string hash, string salt, User user)
    {
        Salt = salt;
        Passwordhash = hash;
        IdNavigation = user;
    }
}

public partial class Quiz
{
    private Quiz()
    {
    }

    public Quiz(string name, User user)
    {
        Id = Guid.NewGuid().ToString();
        Name = name;
        Createdby = user.Id;
        CreatedbyNavigation = user;
    }
}

public partial class Question
{
    private Question()
    {
    }

    public Question(string description, int seconds, Quiz quiz)
    {
        Id = Guid.NewGuid().ToString();
        Description = description;
        Seconds = seconds;
        Quizid = quiz.Id;
        Quiz = quiz;
    }
}

public partial class Option
{
    private Option()
    {
    }

    public Option(string description, bool isCorrect, Question question)
    {
        Id = Guid.NewGuid().ToString();
        Description = description;
        Iscorrect = isCorrect;
        Questionid = question.Id;
        Question = question;
    }
}

public partial class Answer
{
    public Answer(User user, Gameround gameround, Option option, DateTime answeredAt)
    {
        Userid = user.Id;
        Gameround = gameround.Id;
        Option = option.Id;
        Answeredat = answeredAt;
        User = user;
        GameroundNavigation = gameround;
        OptionNavigation = option;
    }

    public Answer(string userId, string gameroundId, string optionId, DateTime answeredAt)
    {
        Userid = userId;
        Gameround = gameroundId;
        Option = optionId;
        Answeredat = answeredAt;
    }

    private Answer()
    {
    }
}

public partial class User
{
    public User(string name)
    {
        Id = Guid.NewGuid().ToString();
        Name = name;
    }

    private User()
    {
    }
}

public partial class Game
{
    public Game(User host, Quiz quiz)
    {
        Id = Guid.NewGuid().ToString();
        Hostid = host.Id;
        Quizid = quiz.Id;
        Host = host;
        Quiz = quiz;
    }

    public Game(string hostId, string quizId)
    {
        Id = Guid.NewGuid().ToString();
        Hostid = hostId;
        Quizid = quizId;
    }

    private Game()
    {
    }
}

public partial class Gameround
{
    public Gameround(Question question, Game game, DateTime startedAt)
    {
        Id = Guid.NewGuid().ToString();
        Questionid = question.Id;
        Gameid = game.Id;
        Question = question;
        Game = game;
        Startedat = startedAt;
    }

    public Gameround(string questionId, string gameId, DateTime startedAt)
    {
        Id = Guid.NewGuid().ToString();
        Questionid = questionId;
        Gameid = gameId;
        Startedat = startedAt;
    }

    private Gameround()
    {
    }
}

public partial class Gamemember
{
    public Gamemember(User user, Game game, DateTime joinedAt)
    {
        Userid = user.Id;
        Gameid = game.Id;
        User = user;
        Game = game;
        Joinedat = joinedAt;
    }

    public Gamemember(string userId, string gameId, DateTime joinedAt)
    {
        Userid = userId;
        Gameid = gameId;
        Joinedat = joinedAt;
    }

    private Gamemember()
    {
    }
}

