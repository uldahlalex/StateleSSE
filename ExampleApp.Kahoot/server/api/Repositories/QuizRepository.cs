using api.Extensions;
using api.Repositories.Abstractions;
using dataaccess;
using Microsoft.EntityFrameworkCore;

namespace api.Repositories;

public class QuizRepository(MyDbContext context) : IQuizRepository
{
    public IQueryable<Quiz> QuizQuery()
    {
        return context.Quizzes; //.IncludeAll();
    }

    public IQueryable<Gameround> GameroundQuery()
    {
        return context.Gamerounds; //.IncludeAll();
    }

    public IQueryable<Game> GameQuery()
    {
        return context.Games; //.IncludeAll();
    }

    public IQueryable<User> UserQuery()
    {
        return context.Users.AsNoTracking(); //.IncludeAll();
    }

    public IQueryable<Question> QuestionQuery()
    {
        return context.Questions; //.IncludeAll();
    }

    public IQueryable<Option> OptionQuery()
    {
        return context.Options; //.IncludeAll();
    }

    public IQueryable<Answer> AnswerQuery()
    {
        return context.Answers; //.IncludeAll();
    }

    public IQueryable<Gamemember> GamememberQuery()
    {
        return context.Gamemembers; //.IncludeAll();
    }



    public void Add<T>(T entity) where T : class
    {
        context.Add(entity);
    }

    public void Remove<T>(T entity) where T : class
    {
        context.Remove(entity);
    }

    public async Task<int> SaveChangesAsync()
    {
        return await context.SaveChangesAsync();
    }

    public int SaveChanges()
    {
        return context.SaveChanges();
    }

    public async Task<Quiz> UpdateQuiz(string id)
    {
        var quiz = context.Quizzes.AsTracking().First(q => q.Id == id);
        quiz.Name = "LOL";
        await context.SaveChangesAsync();
        return quiz;
    }
}