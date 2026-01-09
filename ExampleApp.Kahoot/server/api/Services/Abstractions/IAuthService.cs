using api.Models.RequestDtos;
using api.Models.ReturnDtos;

namespace api.Services.Abstractions;

public interface IAuthService
{
    Task<JwtResponse> Login(AuthRequestDto dto);
    Task<JwtResponse> Register(AuthRequestDto dto);
}