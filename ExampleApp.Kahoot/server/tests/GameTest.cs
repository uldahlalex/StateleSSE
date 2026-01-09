using System.ComponentModel.DataAnnotations;
using api.Etc;
using api.Models.RequestDtos;
using api.Services.Abstractions;
using dataaccess;
using Microsoft.EntityFrameworkCore;

namespace tests;

public class GameTest(
    MyDbContext ctx,
    IQuizService quizService,
    ISeeder iseeder,
    ITestOutputHelper testOutputHelper)
{
    private readonly Seeder seeder = (Seeder)iseeder;


    [Fact]
    public async Task GameFlow()
    {
        await seeder.SeedAsync();
        var host = seeder.User1;
        var player1 = seeder.User2;
        var player2 = seeder.User3;
        var quiz = ctx.Quizzes
                       .Include(q => q.Questions)
                       .ThenInclude(q => q.Options)
                       .FirstOrDefault(q => q.Id == seeder.Quiz1.Id) ??
                   throw new ValidationException("Could not find the seeded quiz");
        var createdGameFromQuiz = await quizService.CreateGame(new CreateGameFromQuizRequestDto
        {
            QuizId = quiz.Id
        }, host.Id);
        var joinGameRequestDto = new JoinGameRequestDto
        {
            GameId = createdGameFromQuiz.Id
        };
        await quizService.JoinGame(joinGameRequestDto, player1.Id);
        await quizService.JoinGame(joinGameRequestDto, player2.Id);
        var nextRoundRequestDto = new NextRoundRequestDto
        {
            GameId = createdGameFromQuiz.Id
        };
        var nextRound = await quizService.CreateNewRound(nextRoundRequestDto,
                            host.Id) ??
                        throw new ValidationException("Expected game to have a next valid round, but didnt");
        var createAnswerRequestDto = new CreateAnswerRequestDto
        {
            OptionId = nextRound.Question.Options.First().Id,
            GameRoundId = nextRound.Id
        };
        var p1Answer = await quizService.SubmitAnswer(createAnswerRequestDto, player1.Id);
        var p2Answer = await quizService.SubmitAnswer(createAnswerRequestDto, player2.Id);
        var endRoundRequestDto = new EndRoundRequestDto
        {
            RoundId = nextRound.Id
        };
        var endRoundRequest = await quizService.EndRound(endRoundRequestDto, host.Id);
        // Assert.Equal(2, endRoundRequest.Answers.Count);
    }
}