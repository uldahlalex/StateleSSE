using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace server;

public static class JwtService
{
    public static readonly SymmetricSecurityKey Key =
        new("YourSuperSecretKeyAtLeast32Bytes!"u8.ToArray());

    public static string GenerateToken(string userId, string userName) =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            claims: [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.NameIdentifier, userName)],
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: new SigningCredentials(Key, SecurityAlgorithms.HmacSha256)));
}
