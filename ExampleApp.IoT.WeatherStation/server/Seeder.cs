using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace server;

public class Seeder(MyDbContext ctx, ILogger<Seeder> logger)
{
    public void Seed()
    {
        logger.LogInformation("adding a test user + pasting the recreate script for reproducing the schema");
        logger.LogInformation(ctx.Database.GenerateCreateScript());
        var exists = ctx.Users.Any(u => u.Id == "test");
        if (!exists)
        {

            var salt = "word";
            var password = "pass";
            ctx.Users.Add(new User()
            {
                Id = Guid.NewGuid().ToString(),
                Nickname = "test",
                //password is "pass", salt is "word"
                Hash = Convert.ToBase64String(
                    SHA256.HashData(
                        Encoding.UTF8.GetBytes(password + salt))),
                Salt = salt,
            });
            ctx.SaveChanges();
        }
    }
}