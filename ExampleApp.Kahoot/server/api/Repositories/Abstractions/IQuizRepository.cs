using dataaccess;

namespace api.Repositories.Abstractions;

public interface IQuizRepository
{
    IQueryable<Quiz> QuizQuery();
    IQueryable<Game> GameQuery();
    IQueryable<Gameround> GameroundQuery();
    IQueryable<User> UserQuery();
    IQueryable<Question> QuestionQuery();
    IQueryable<Option> OptionQuery();
    IQueryable<Answer> AnswerQuery();
    IQueryable<Gamemember> GamememberQuery();
    void Add<T>(T entity) where T : class;
    void Remove<T>(T entity) where T : class;
    Task<int> SaveChangesAsync();
    int SaveChanges();
    Task<Quiz> UpdateQuiz(string id);
}