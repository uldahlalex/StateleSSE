using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace server;

public class JwtService( IConfiguration configuration)
{

    public string GenerateToken(string userId)
    {
        var secret = configuration.GetSection("Secret").Value ??
                     throw new InvalidOperationException("JWT Secret not configured");
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            claims: [new Claim(ClaimTypes.NameIdentifier, userId)],
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256)));
    }
}
