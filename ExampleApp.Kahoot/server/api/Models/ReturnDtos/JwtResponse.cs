using Tapper;

namespace api.Models.ReturnDtos;

[TranspilationSource]
public record JwtResponse(string Token)
{
    public string Token { get; set; } = Token;
}