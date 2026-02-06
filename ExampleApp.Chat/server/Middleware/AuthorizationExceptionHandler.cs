using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace server;

public class AuthorizationExceptionHandler : IAuthorizationMiddlewareResultHandler
{
    public Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged)
            throw new UnauthorizedAccessException("Authentication required");

        if (authorizeResult.Forbidden)
            throw new UnauthorizedAccessException("Access forbidden");

        return next(context);
    }
}
