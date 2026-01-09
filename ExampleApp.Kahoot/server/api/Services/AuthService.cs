using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using api.Models;
using api.Models.RequestDtos;
using api.Models.ReturnDtos;
using api.Services.Abstractions;
using dataaccess;
using JWT;
using JWT.Algorithms;
using JWT.Builder;
using JWT.Serializers;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public class AuthService(
    MyDbContext ctx,
    ILogger<AuthService> logger,
    TimeProvider timeProvider,
    MyAppOptions appOptions) : IAuthService
{
    public async Task<JwtResponse> Login(AuthRequestDto dto)
    {
        var user = ctx.Users
                       .Include(u => u.Userscredential)
                       .FirstOrDefault(u => u.Name == dto.Name)
                   ?? throw new ValidationException("User is not found!");

        if (user.Userscredential == null)
            throw new ValidationException("User credentials not found!");

        var passwordsMatch = user.Userscredential.Passwordhash ==
                             SHA512.HashData(
                                     Encoding.UTF8.GetBytes(dto.Password + user.Userscredential.Salt))
                                 .Aggregate("", (current, b) => current + b.ToString("x2"));
        if (!passwordsMatch)
            throw new ValidationException("Password is incorrect!");

        var token = CreateJwt(user);
        return new JwtResponse(token);
    }

    public async Task<JwtResponse> Register(AuthRequestDto dto)
    {
        Validator.ValidateObject(dto, new ValidationContext(dto), true);

        var isNameTaken = ctx.Users.Any(u => u.Name == dto.Name);
        if (isNameTaken)
            throw new ValidationException("Email is already taken");

        var salt = Guid.NewGuid().ToString();
        var hash = SHA512.HashData(
            Encoding.UTF8.GetBytes(dto.Password + salt));
        var passwordHash = hash.Aggregate("", (current, b) => current + b.ToString("x2"));

        var user = new User(dto.Name);

        var userCredential = new Userscredential(passwordHash, salt, user);

        ctx.Users.Add(user);
        ctx.Userscredentials.Add(userCredential);
        await ctx.SaveChangesAsync();

        var token = CreateJwt(user);
        return new JwtResponse(token);
    }

    public async Task<JwtClaims> VerifyAndDecodeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ValidationException("No token attached!");

        var builder = CreateJwtBuilder();

        string jsonString;
        try
        {
            jsonString = builder.Decode(token)
                         ?? throw new ValidationException("Authentication failed!");
        }
        catch (Exception e)
        {
            logger.LogError(e.Message, e);
            throw new ValidationException("Valided to verify JWT");
        }

        var jwtClaims = JsonSerializer.Deserialize<JwtClaims>(jsonString, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new ValidationException("Authentication failed!");

        _ = ctx.Users.FirstOrDefault(u => u.Id == jwtClaims.Id)
            ?? throw new ValidationException("User is not found!");

        return jwtClaims;
    }

    private JwtBuilder CreateJwtBuilder()
    {
        return JwtBuilder.Create()
            .WithAlgorithm(new HMACSHA512Algorithm())
            .WithSecret(Encoding.UTF8.GetBytes(appOptions.JwtSecret))
            .WithUrlEncoder(new JwtBase64UrlEncoder())
            .WithJsonSerializer(new JsonNetSerializer())
            .MustVerifySignature();
    }

    private string CreateJwt(User user)
    {
        return CreateJwtBuilder()
            .AddClaim(nameof(User.Id), user.Id)
            .Encode();
    }
}