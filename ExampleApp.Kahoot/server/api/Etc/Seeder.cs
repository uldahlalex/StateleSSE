using System.Security.Cryptography;
using System.Text;
using dataaccess;

namespace api.Etc;

public class Seeder(MyDbContext ctx, TimeProvider timeProvider) : ISeeder
{
    // ============ USERS ============

    /// <summary>Id="user-1", Name="Alice", Password=hash("pass")</summary>
    public User User1 { get; private set; } = null!;

    /// <summary>Id="user-2", Name="Bob", Password=hash("pass")</summary>
    public User User2 { get; private set; } = null!;

    /// <summary>Id="user-3", Name="Charlie", Password=hash("pass")</summary>
    public User User3 { get; private set; } = null!;

    // ============ QUIZZES ============

    /// <summary>Id="quiz-1", Title="General Knowledge Quiz", Owner=User1(Alice)</summary>
    public Quiz Quiz1 { get; private set; } = null!;

    /// <summary>Id="quiz-2", Title="Science Quiz", Owner=User2(Bob)</summary>
    public Quiz Quiz2 { get; private set; } = null!;

    // ============ QUESTIONS ============

    /// <summary>Id="q1", Text="What is 2+2?", TimeLimit=30s, Quiz=Quiz1</summary>
    public Question Q1 { get; private set; } = null!;

    /// <summary>Id="q2", Text="What is the capital of France?", TimeLimit=45s, Quiz=Quiz1</summary>
    public Question Q2 { get; private set; } = null!;

    /// <summary>Id="q3", Text="Which planet is known as the Red Planet?", TimeLimit=30s, Quiz=Quiz1</summary>
    public Question Q3 { get; private set; } = null!;

    /// <summary>Id="q4", Text="What is H2O?", TimeLimit=20s, Quiz=Quiz2</summary>
    public Question Q4 { get; private set; } = null!;

    // Additional questions for Quiz1 (General Knowledge)

    /// <summary>Id="q6", Text="Who painted the Mona Lisa?", TimeLimit=30s, Quiz=Quiz1</summary>
    public Question Q6 { get; private set; } = null!;

    /// <summary>Id="q7", Text="What is the largest ocean on Earth?", TimeLimit=25s, Quiz=Quiz1</summary>
    public Question Q7 { get; private set; } = null!;

    /// <summary>Id="q8", Text="In which year did World War II end?", TimeLimit=35s, Quiz=Quiz1</summary>
    public Question Q8 { get; private set; } = null!;

    /// <summary>Id="q9", Text="What is the chemical symbol for gold?", TimeLimit=20s, Quiz=Quiz1</summary>
    public Question Q9 { get; private set; } = null!;

    /// <summary>Id="q10", Text="Who wrote 'Romeo and Juliet'?", TimeLimit=30s, Quiz=Quiz1</summary>
    public Question Q10 { get; private set; } = null!;

    /// <summary>Id="q11", Text="What is the tallest mountain in the world?", TimeLimit=25s, Quiz=Quiz1</summary>
    public Question Q11 { get; private set; } = null!;

    /// <summary>Id="q12", Text="How many continents are there?", TimeLimit=20s, Quiz=Quiz1</summary>
    public Question Q12 { get; private set; } = null!;

    /// <summary>Id="q13", Text="What is the largest mammal in the world?", TimeLimit=25s, Quiz=Quiz1</summary>
    public Question Q13 { get; private set; } = null!;

    /// <summary>Id="q14", Text="In which country is the Great Pyramid of Giza?", TimeLimit=30s, Quiz=Quiz1</summary>
    public Question Q14 { get; private set; } = null!;

    /// <summary>Id="q15", Text="What is the hardest natural substance on Earth?", TimeLimit=30s, Quiz=Quiz1</summary>
    public Question Q15 { get; private set; } = null!;

    /// <summary>Id="q16", Text="Who was the first person to walk on the moon?", TimeLimit=30s, Quiz=Quiz1</summary>
    public Question Q16 { get; private set; } = null!;

    /// <summary>Id="q17", Text="What is the smallest country in the world?", TimeLimit=30s, Quiz=Quiz1</summary>
    public Question Q17 { get; private set; } = null!;

    /// <summary>Id="q18", Text="How many sides does a hexagon have?", TimeLimit=20s, Quiz=Quiz1</summary>
    public Question Q18 { get; private set; } = null!;

    /// <summary>Id="q19", Text="What is the capital of Japan?", TimeLimit=25s, Quiz=Quiz1</summary>
    public Question Q19 { get; private set; } = null!;

    /// <summary>Id="q20", Text="What year did the Titanic sink?", TimeLimit=35s, Quiz=Quiz1</summary>
    public Question Q20 { get; private set; } = null!;

    /// <summary>Id="q21", Text="What is the largest planet in our solar system?", TimeLimit=25s, Quiz=Quiz1</summary>
    public Question Q21 { get; private set; } = null!;

    /// <summary>Id="q22", Text="Who invented the telephone?", TimeLimit=30s, Quiz=Quiz1</summary>
    public Question Q22 { get; private set; } = null!;

    /// <summary>Id="q5", Text="What is the speed of light?", TimeLimit=60s, Quiz=Quiz2</summary>
    public Question Q5 { get; private set; } = null!;

    // ============ OPTIONS ============

    /// <summary>Id="opt1", Text="3", IsCorrect=false, Question=Q1</summary>
    public Option Opt1 { get; private set; } = null!;

    /// <summary>Id="opt2", Text="4", IsCorrect=TRUE, Question=Q1</summary>
    public Option Opt2 { get; private set; } = null!;

    /// <summary>Id="opt3", Text="5", IsCorrect=false, Question=Q1</summary>
    public Option Opt3 { get; private set; } = null!;

    /// <summary>Id="opt4", Text="22", IsCorrect=false, Question=Q1</summary>
    public Option Opt4 { get; private set; } = null!;

    /// <summary>Id="opt5", Text="London", IsCorrect=false, Question=Q2</summary>
    public Option Opt5 { get; private set; } = null!;

    /// <summary>Id="opt6", Text="Paris", IsCorrect=TRUE, Question=Q2</summary>
    public Option Opt6 { get; private set; } = null!;

    /// <summary>Id="opt7", Text="Berlin", IsCorrect=false, Question=Q2</summary>
    public Option Opt7 { get; private set; } = null!;

    /// <summary>Id="opt8", Text="Madrid", IsCorrect=false, Question=Q2</summary>
    public Option Opt8 { get; private set; } = null!;

    /// <summary>Id="opt9", Text="Venus", IsCorrect=false, Question=Q3</summary>
    public Option Opt9 { get; private set; } = null!;

    /// <summary>Id="opt10", Text="Mars", IsCorrect=TRUE, Question=Q3</summary>
    public Option Opt10 { get; private set; } = null!;

    /// <summary>Id="opt11", Text="Jupiter", IsCorrect=false, Question=Q3</summary>
    public Option Opt11 { get; private set; } = null!;

    /// <summary>Id="opt12", Text="Saturn", IsCorrect=false, Question=Q3</summary>
    public Option Opt12 { get; private set; } = null!;

    /// <summary>Id="opt13", Text="Water", IsCorrect=TRUE, Question=Q4</summary>
    public Option Opt13 { get; private set; } = null!;

    /// <summary>Id="opt14", Text="Oxygen", IsCorrect=false, Question=Q4</summary>
    public Option Opt14 { get; private set; } = null!;

    /// <summary>Id="opt15", Text="Hydrogen", IsCorrect=false, Question=Q4</summary>
    public Option Opt15 { get; private set; } = null!;

    /// <summary>Id="opt16", Text="Carbon Dioxide", IsCorrect=false, Question=Q4</summary>
    public Option Opt16 { get; private set; } = null!;

    /// <summary>Id="opt17", Text="299,792,458 m/s", IsCorrect=TRUE, Question=Q5</summary>
    public Option Opt17 { get; private set; } = null!;

    /// <summary>Id="opt18", Text="150,000,000 m/s", IsCorrect=false, Question=Q5</summary>
    public Option Opt18 { get; private set; } = null!;

    /// <summary>Id="opt19", Text="500,000,000 m/s", IsCorrect=false, Question=Q5</summary>
    public Option Opt19 { get; private set; } = null!;

    /// <summary>Id="opt20", Text="100,000,000 m/s", IsCorrect=false, Question=Q5</summary>
    public Option Opt20 { get; private set; } = null!;

    // Q6 options - "Who painted the Mona Lisa?"
    public Option Opt21 { get; private set; } = null!;
    public Option Opt22 { get; private set; } = null!;
    public Option Opt23 { get; private set; } = null!;
    public Option Opt24 { get; private set; } = null!;

    // Q7 options - "What is the largest ocean on Earth?"
    public Option Opt25 { get; private set; } = null!;
    public Option Opt26 { get; private set; } = null!;
    public Option Opt27 { get; private set; } = null!;
    public Option Opt28 { get; private set; } = null!;

    // Q8 options - "In which year did World War II end?"
    public Option Opt29 { get; private set; } = null!;
    public Option Opt30 { get; private set; } = null!;
    public Option Opt31 { get; private set; } = null!;
    public Option Opt32 { get; private set; } = null!;

    // Q9 options - "What is the chemical symbol for gold?"
    public Option Opt33 { get; private set; } = null!;
    public Option Opt34 { get; private set; } = null!;
    public Option Opt35 { get; private set; } = null!;
    public Option Opt36 { get; private set; } = null!;

    // Q10 options - "Who wrote 'Romeo and Juliet'?"
    public Option Opt37 { get; private set; } = null!;
    public Option Opt38 { get; private set; } = null!;
    public Option Opt39 { get; private set; } = null!;
    public Option Opt40 { get; private set; } = null!;

    // Q11 options - "What is the tallest mountain in the world?"
    public Option Opt41 { get; private set; } = null!;
    public Option Opt42 { get; private set; } = null!;
    public Option Opt43 { get; private set; } = null!;
    public Option Opt44 { get; private set; } = null!;

    // Q12 options - "How many continents are there?"
    public Option Opt45 { get; private set; } = null!;
    public Option Opt46 { get; private set; } = null!;
    public Option Opt47 { get; private set; } = null!;
    public Option Opt48 { get; private set; } = null!;

    // Q13 options - "What is the largest mammal in the world?"
    public Option Opt49 { get; private set; } = null!;
    public Option Opt50 { get; private set; } = null!;
    public Option Opt51 { get; private set; } = null!;
    public Option Opt52 { get; private set; } = null!;

    // Q14 options - "In which country is the Great Pyramid of Giza?"
    public Option Opt53 { get; private set; } = null!;
    public Option Opt54 { get; private set; } = null!;
    public Option Opt55 { get; private set; } = null!;
    public Option Opt56 { get; private set; } = null!;

    // Q15 options - "What is the hardest natural substance on Earth?"
    public Option Opt57 { get; private set; } = null!;
    public Option Opt58 { get; private set; } = null!;
    public Option Opt59 { get; private set; } = null!;
    public Option Opt60 { get; private set; } = null!;

    // Q16 options - "Who was the first person to walk on the moon?"
    public Option Opt61 { get; private set; } = null!;
    public Option Opt62 { get; private set; } = null!;
    public Option Opt63 { get; private set; } = null!;
    public Option Opt64 { get; private set; } = null!;

    // Q17 options - "What is the smallest country in the world?"
    public Option Opt65 { get; private set; } = null!;
    public Option Opt66 { get; private set; } = null!;
    public Option Opt67 { get; private set; } = null!;
    public Option Opt68 { get; private set; } = null!;

    // Q18 options - "How many sides does a hexagon have?"
    public Option Opt69 { get; private set; } = null!;
    public Option Opt70 { get; private set; } = null!;
    public Option Opt71 { get; private set; } = null!;
    public Option Opt72 { get; private set; } = null!;

    // Q19 options - "What is the capital of Japan?"
    public Option Opt73 { get; private set; } = null!;
    public Option Opt74 { get; private set; } = null!;
    public Option Opt75 { get; private set; } = null!;
    public Option Opt76 { get; private set; } = null!;

    // Q20 options - "What year did the Titanic sink?"
    public Option Opt77 { get; private set; } = null!;
    public Option Opt78 { get; private set; } = null!;
    public Option Opt79 { get; private set; } = null!;
    public Option Opt80 { get; private set; } = null!;

    // Q21 options - "What is the largest planet in our solar system?"
    public Option Opt81 { get; private set; } = null!;
    public Option Opt82 { get; private set; } = null!;
    public Option Opt83 { get; private set; } = null!;
    public Option Opt84 { get; private set; } = null!;

    // Q22 options - "Who invented the telephone?"
    public Option Opt85 { get; private set; } = null!;
    public Option Opt86 { get; private set; } = null!;
    public Option Opt87 { get; private set; } = null!;
    public Option Opt88 { get; private set; } = null!;

    // ============ GAMES ============

    /// <summary>Id="game-1", Host=User1(Alice), Quiz=Quiz1, Status=InProgress</summary>
    public Game Game1 { get; private set; } = null!;

    /// <summary>Id="game-2", Host=User2(Bob), Quiz=Quiz2, Status=Started</summary>
    public Game Game2 { get; private set; } = null!;

    // ============ GAME MEMBERS ============

    /// <summary>User=User1(Alice), Game=Game1, JoinedAt=now-10min</summary>
    public Gamemember Gm1 { get; private set; } = null!;

    /// <summary>User=User2(Bob), Game=Game1, JoinedAt=now-9min</summary>
    public Gamemember Gm2 { get; private set; } = null!;

    /// <summary>User=User3(Charlie), Game=Game1, JoinedAt=now-8min</summary>
    public Gamemember Gm3 { get; private set; } = null!;

    /// <summary>User=User2(Bob), Game=Game2, JoinedAt=now-5min</summary>

    /// <summary>User=User3(Charlie), Game=Game2, JoinedAt=now-4min</summary>
    public Gamemember Gm5 { get; private set; } = null!;

    // ============ GAME ROUNDS ============

    /// <summary>Id="round-1", Question=Q1("What is 2+2?"), Game=Game1, StartedAt=now-7min</summary>
    public Gameround Round1 { get; private set; } = null!;

    /// <summary>Id="round-2", Question=Q2("What is the capital of France?"), Game=Game1, StartedAt=now-6min</summary>
    public Gameround Round2 { get; private set; } = null!;

    /// <summary>Id="round-3", Question=Q4("What is H2O?"), Game=Game2, StartedAt=now-3min</summary>
    public Gameround Round3 { get; private set; } = null!;

    // ============ ANSWERS ============

    /// <summary>User=User1(Alice), Round=Round1, Option=Opt2("4"-CORRECT), AnsweredAt=now-6min-50s</summary>
    public Answer Ans1 { get; private set; } = null!;

    /// <summary>User=User2(Bob), Round=Round1, Option=Opt3("5"-wrong), AnsweredAt=now-6min-45s</summary>
    public Answer Ans2 { get; private set; } = null!;

    /// <summary>User=User3(Charlie), Round=Round1, Option=Opt2("4"-CORRECT), AnsweredAt=now-6min-48s</summary>
    public Answer Ans3 { get; private set; } = null!;

    /// <summary>User=User1(Alice), Round=Round2, Option=Opt6("Paris"-CORRECT), AnsweredAt=now-5min-30s</summary>
    public Answer Ans4 { get; private set; } = null!;

    /// <summary>User=User2(Bob), Round=Round2, Option=Opt6("Paris"-CORRECT), AnsweredAt=now-5min-25s</summary>
    public Answer Ans5 { get; private set; } = null!;

    /// <summary>User=User2(Bob), Round=Round3, Option=Opt13("Water"-CORRECT), AnsweredAt=now-2min-15s</summary>
    public Answer Ans6 { get; private set; } = null!;

    public async Task SeedAsync()
    {
        ctx.Database.EnsureCreated();
        ctx.RemoveRange(ctx.Answers);
        ctx.RemoveRange(ctx.Gamerounds);
        ctx.RemoveRange(ctx.Gamemembers);
        ctx.RemoveRange(ctx.Games);
        ctx.RemoveRange(ctx.Options);
        ctx.RemoveRange(ctx.Questions);
        ctx.RemoveRange(ctx.Quizzes);
        ctx.RemoveRange(ctx.Userscredentials);
        ctx.RemoveRange(ctx.Users);

        var now = timeProvider.GetUtcNow().UtcDateTime;

        var salt = "";
        var hash = SHA512.HashData(Encoding.UTF8.GetBytes("pass" + salt))
            .Aggregate("", (current, b) => current + b.ToString("x2"));

        // Create users
        User1 = new User("Alice") { Id = "user-1" };
        User2 = new User("Bob") { Id = "user-2" };
        User3 = new User("Charlie") { Id = "user-3" };

        var user1Cred = new Userscredential(hash, salt, User1) { Id = "user-1", Salt = salt, Passwordhash = hash };
        var user2Cred = new Userscredential(hash, salt, User2) { Id = "user-2", Salt = salt, Passwordhash = hash };
        var user3Cred = new Userscredential(hash, salt, User3) { Id = "user-3", Salt = salt, Passwordhash = hash };

        // Create quizzes
        Quiz1 = new Quiz("General Knowledge Quiz", User1) { Id = "quiz-1" };
        Quiz2 = new Quiz("Science Quiz", User2) { Id = "quiz-2" };

        // Create questions for quiz 1
        Q1 = new Question("What is 2+2?", 30, Quiz1) { Id = "q1" };
        Q2 = new Question("What is the capital of France?", 45, Quiz1) { Id = "q2" };
        Q3 = new Question("Which planet is known as the Red Planet?", 30, Quiz1) { Id = "q3" };
        Q6 = new Question("Who painted the Mona Lisa?", 30, Quiz1) { Id = "q6" };
        Q7 = new Question("What is the largest ocean on Earth?", 25, Quiz1) { Id = "q7" };
        Q8 = new Question("In which year did World War II end?", 35, Quiz1) { Id = "q8" };
        Q9 = new Question("What is the chemical symbol for gold?", 20, Quiz1) { Id = "q9" };
        Q10 = new Question("Who wrote 'Romeo and Juliet'?", 30, Quiz1) { Id = "q10" };
        Q11 = new Question("What is the tallest mountain in the world?", 25, Quiz1) { Id = "q11" };
        Q12 = new Question("How many continents are there?", 20, Quiz1) { Id = "q12" };
        Q13 = new Question("What is the largest mammal in the world?", 25, Quiz1) { Id = "q13" };
        Q14 = new Question("In which country is the Great Pyramid of Giza?", 30, Quiz1) { Id = "q14" };
        Q15 = new Question("What is the hardest natural substance on Earth?", 30, Quiz1) { Id = "q15" };
        Q16 = new Question("Who was the first person to walk on the moon?", 30, Quiz1) { Id = "q16" };
        Q17 = new Question("What is the smallest country in the world?", 30, Quiz1) { Id = "q17" };
        Q18 = new Question("How many sides does a hexagon have?", 20, Quiz1) { Id = "q18" };
        Q19 = new Question("What is the capital of Japan?", 25, Quiz1) { Id = "q19" };
        Q20 = new Question("What year did the Titanic sink?", 35, Quiz1) { Id = "q20" };
        Q21 = new Question("What is the largest planet in our solar system?", 25, Quiz1) { Id = "q21" };
        Q22 = new Question("Who invented the telephone?", 30, Quiz1) { Id = "q22" };

        // Create questions for quiz 2
        Q4 = new Question("What is H2O?", 20, Quiz2) { Id = "q4" };
        Q5 = new Question("What is the speed of light?", 60, Quiz2) { Id = "q5" };

        // Create options for questions
        // Q1 options
        Opt1 = new Option("3", false, Q1) { Id = "opt1" };
        Opt2 = new Option("4", true, Q1) { Id = "opt2" };
        Opt3 = new Option("5", false, Q1) { Id = "opt3" };
        Opt4 = new Option("22", false, Q1) { Id = "opt4" };

        // Q2 options
        Opt5 = new Option("London", false, Q2) { Id = "opt5" };
        Opt6 = new Option("Paris", true, Q2) { Id = "opt6" };
        Opt7 = new Option("Berlin", false, Q2) { Id = "opt7" };
        Opt8 = new Option("Madrid", false, Q2) { Id = "opt8" };

        // Q3 options
        Opt9 = new Option("Venus", false, Q3) { Id = "opt9" };
        Opt10 = new Option("Mars", true, Q3) { Id = "opt10" };
        Opt11 = new Option("Jupiter", false, Q3) { Id = "opt11" };
        Opt12 = new Option("Saturn", false, Q3) { Id = "opt12" };

        // Q4 options
        Opt13 = new Option("Water", true, Q4) { Id = "opt13" };
        Opt14 = new Option("Oxygen", false, Q4) { Id = "opt14" };
        Opt15 = new Option("Hydrogen", false, Q4) { Id = "opt15" };
        Opt16 = new Option("Carbon Dioxide", false, Q4) { Id = "opt16" };

        // Q5 options
        Opt17 = new Option("299,792,458 m/s", true, Q5) { Id = "opt17" };
        Opt18 = new Option("150,000,000 m/s", false, Q5) { Id = "opt18" };
        Opt19 = new Option("500,000,000 m/s", false, Q5) { Id = "opt19" };
        Opt20 = new Option("100,000,000 m/s", false, Q5) { Id = "opt20" };

        // Q6 options - "Who painted the Mona Lisa?"
        Opt21 = new Option("Leonardo da Vinci", true, Q6) { Id = "opt21" };
        Opt22 = new Option("Pablo Picasso", false, Q6) { Id = "opt22" };
        Opt23 = new Option("Vincent van Gogh", false, Q6) { Id = "opt23" };
        Opt24 = new Option("Michelangelo", false, Q6) { Id = "opt24" };

        // Q7 options - "What is the largest ocean on Earth?"
        Opt25 = new Option("Pacific Ocean", true, Q7) { Id = "opt25" };
        Opt26 = new Option("Atlantic Ocean", false, Q7) { Id = "opt26" };
        Opt27 = new Option("Indian Ocean", false, Q7) { Id = "opt27" };
        Opt28 = new Option("Arctic Ocean", false, Q7) { Id = "opt28" };

        // Q8 options - "In which year did World War II end?"
        Opt29 = new Option("1945", true, Q8) { Id = "opt29" };
        Opt30 = new Option("1918", false, Q8) { Id = "opt30" };
        Opt31 = new Option("1944", false, Q8) { Id = "opt31" };
        Opt32 = new Option("1946", false, Q8) { Id = "opt32" };

        // Q9 options - "What is the chemical symbol for gold?"
        Opt33 = new Option("Au", true, Q9) { Id = "opt33" };
        Opt34 = new Option("Ag", false, Q9) { Id = "opt34" };
        Opt35 = new Option("Go", false, Q9) { Id = "opt35" };
        Opt36 = new Option("Gd", false, Q9) { Id = "opt36" };

        // Q10 options - "Who wrote 'Romeo and Juliet'?"
        Opt37 = new Option("William Shakespeare", true, Q10) { Id = "opt37" };
        Opt38 = new Option("Charles Dickens", false, Q10) { Id = "opt38" };
        Opt39 = new Option("Jane Austen", false, Q10) { Id = "opt39" };
        Opt40 = new Option("Mark Twain", false, Q10) { Id = "opt40" };

        // Q11 options - "What is the tallest mountain in the world?"
        Opt41 = new Option("Mount Everest", true, Q11) { Id = "opt41" };
        Opt42 = new Option("K2", false, Q11) { Id = "opt42" };
        Opt43 = new Option("Mount Kilimanjaro", false, Q11) { Id = "opt43" };
        Opt44 = new Option("Mount Fuji", false, Q11) { Id = "opt44" };

        // Q12 options - "How many continents are there?"
        Opt45 = new Option("7", true, Q12) { Id = "opt45" };
        Opt46 = new Option("5", false, Q12) { Id = "opt46" };
        Opt47 = new Option("6", false, Q12) { Id = "opt47" };
        Opt48 = new Option("8", false, Q12) { Id = "opt48" };

        // Q13 options - "What is the largest mammal in the world?"
        Opt49 = new Option("Blue Whale", true, Q13) { Id = "opt49" };
        Opt50 = new Option("African Elephant", false, Q13) { Id = "opt50" };
        Opt51 = new Option("Giraffe", false, Q13) { Id = "opt51" };
        Opt52 = new Option("Polar Bear", false, Q13) { Id = "opt52" };

        // Q14 options - "In which country is the Great Pyramid of Giza?"
        Opt53 = new Option("Egypt", true, Q14) { Id = "opt53" };
        Opt54 = new Option("Mexico", false, Q14) { Id = "opt54" };
        Opt55 = new Option("Peru", false, Q14) { Id = "opt55" };
        Opt56 = new Option("Sudan", false, Q14) { Id = "opt56" };

        // Q15 options - "What is the hardest natural substance on Earth?"
        Opt57 = new Option("Diamond", true, Q15) { Id = "opt57" };
        Opt58 = new Option("Steel", false, Q15) { Id = "opt58" };
        Opt59 = new Option("Granite", false, Q15) { Id = "opt59" };
        Opt60 = new Option("Titanium", false, Q15) { Id = "opt60" };

        // Q16 options - "Who was the first person to walk on the moon?"
        Opt61 = new Option("Neil Armstrong", true, Q16) { Id = "opt61" };
        Opt62 = new Option("Buzz Aldrin", false, Q16) { Id = "opt62" };
        Opt63 = new Option("Yuri Gagarin", false, Q16) { Id = "opt63" };
        Opt64 = new Option("John Glenn", false, Q16) { Id = "opt64" };

        // Q17 options - "What is the smallest country in the world?"
        Opt65 = new Option("Vatican City", true, Q17) { Id = "opt65" };
        Opt66 = new Option("Monaco", false, Q17) { Id = "opt66" };
        Opt67 = new Option("San Marino", false, Q17) { Id = "opt67" };
        Opt68 = new Option("Liechtenstein", false, Q17) { Id = "opt68" };

        // Q18 options - "How many sides does a hexagon have?"
        Opt69 = new Option("6", true, Q18) { Id = "opt69" };
        Opt70 = new Option("5", false, Q18) { Id = "opt70" };
        Opt71 = new Option("7", false, Q18) { Id = "opt71" };
        Opt72 = new Option("8", false, Q18) { Id = "opt72" };

        // Q19 options - "What is the capital of Japan?"
        Opt73 = new Option("Tokyo", true, Q19) { Id = "opt73" };
        Opt74 = new Option("Kyoto", false, Q19) { Id = "opt74" };
        Opt75 = new Option("Osaka", false, Q19) { Id = "opt75" };
        Opt76 = new Option("Seoul", false, Q19) { Id = "opt76" };

        // Q20 options - "What year did the Titanic sink?"
        Opt77 = new Option("1912", true, Q20) { Id = "opt77" };
        Opt78 = new Option("1905", false, Q20) { Id = "opt78" };
        Opt79 = new Option("1920", false, Q20) { Id = "opt79" };
        Opt80 = new Option("1898", false, Q20) { Id = "opt80" };

        // Q21 options - "What is the largest planet in our solar system?"
        Opt81 = new Option("Jupiter", true, Q21) { Id = "opt81" };
        Opt82 = new Option("Saturn", false, Q21) { Id = "opt82" };
        Opt83 = new Option("Neptune", false, Q21) { Id = "opt83" };
        Opt84 = new Option("Earth", false, Q21) { Id = "opt84" };

        // Q22 options - "Who invented the telephone?"
        Opt85 = new Option("Alexander Graham Bell", true, Q22) { Id = "opt85" };
        Opt86 = new Option("Thomas Edison", false, Q22) { Id = "opt86" };
        Opt87 = new Option("Nikola Tesla", false, Q22) { Id = "opt87" };
        Opt88 = new Option("Guglielmo Marconi", false, Q22) { Id = "opt88" };

        // Create games
        Game1 = new Game(User1, Quiz1) { Id = "game-1", Host = User1 };
        Game2 = new Game(User2, Quiz2) { Id = "game-2" };

        // Create game members
        Gm1 = new Gamemember(User1, Game1, now.AddMinutes(-10));
        Gm2 = new Gamemember(User2, Game1, now.AddMinutes(-9));
        Gm3 = new Gamemember(User3, Game1, now.AddMinutes(-8));
        Gm5 = new Gamemember(User3, Game2, now.AddMinutes(-4));

        // Create game rounds (game 1 is in progress, game 2 just started)
        Round1 = new Gameround(Q1, Game1, now.AddMinutes(-7))
        {
            Id = "round-1"
        };

        Round2 = new Gameround(Q2, Game1, now.AddMinutes(-6))
        {
            Id = "round-2"
        };

        Round3 = new Gameround(Q4, Game2, now.AddMinutes(-3))
        {
            Id = "round-3"
        };

        // Create answers for rounds (users who have answered with their option selections)
        // Round 1 - all 3 users answered question q1 ("What is 2+2?")
        Ans1 = new Answer(User1, Round1, Opt2, now.AddMinutes(-6).AddSeconds(-50)); // Alice: "4" (correct)
        Ans2 = new Answer(User2, Round1, Opt3, now.AddMinutes(-6).AddSeconds(-45)); // Bob: "5" (wrong)
        Ans3 = new Answer(User3, Round1, Opt2, now.AddMinutes(-6).AddSeconds(-48)); // Charlie: "4" (correct)

        // Round 2 - 2 users answered question q2 ("What is the capital of France?")
        Ans4 = new Answer(User1, Round2, Opt6, now.AddMinutes(-5).AddSeconds(-30)); // Alice: "Paris" (correct)
        Ans5 = new Answer(User2, Round2, Opt6, now.AddMinutes(-5).AddSeconds(-25)); // Bob: "Paris" (correct)

        // Round 3 - 1 user answered question q4 ("What is H2O?")
        Ans6 = new Answer(User2, Round3, Opt13, now.AddMinutes(-2).AddSeconds(-15)); // Bob: "Water" (correct)

        // Add entities to context
        ctx.Users.AddRange(User1, User2, User3);
        ctx.Userscredentials.AddRange(user1Cred, user2Cred, user3Cred);
        ctx.Quizzes.AddRange(Quiz1, Quiz2);
        ctx.Questions.AddRange(Q1, Q2, Q3, Q4, Q5, Q6, Q7, Q8, Q9, Q10, Q11, Q12, Q13, Q14, Q15,
            Q16, Q17, Q18, Q19, Q20, Q21, Q22);
        ctx.Options.AddRange(Opt1, Opt2, Opt3, Opt4, Opt5, Opt6, Opt7, Opt8, Opt9, Opt10,
            Opt11, Opt12, Opt13, Opt14, Opt15, Opt16, Opt17, Opt18, Opt19, Opt20,
            Opt21, Opt22, Opt23, Opt24, Opt25, Opt26, Opt27, Opt28, Opt29, Opt30,
            Opt31, Opt32, Opt33, Opt34, Opt35, Opt36, Opt37, Opt38, Opt39, Opt40,
            Opt41, Opt42, Opt43, Opt44, Opt45, Opt46, Opt47, Opt48, Opt49, Opt50,
            Opt51, Opt52, Opt53, Opt54, Opt55, Opt56, Opt57, Opt58, Opt59, Opt60,
            Opt61, Opt62, Opt63, Opt64, Opt65, Opt66, Opt67, Opt68, Opt69, Opt70,
            Opt71, Opt72, Opt73, Opt74, Opt75, Opt76, Opt77, Opt78, Opt79, Opt80,
            Opt81, Opt82, Opt83, Opt84, Opt85, Opt86, Opt87, Opt88);
        ctx.Games.AddRange(Game1, Game2);
        ctx.Gamemembers.AddRange(Gm1, Gm2, Gm3, Gm5);
        ctx.Gamerounds.AddRange(Round1, Round2, Round3);
        ctx.Answers.AddRange(Ans1, Ans2, Ans3, Ans4, Ans5, Ans6);

        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
    }
}