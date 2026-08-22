using System;
using System.Security.Claims;
using CleanArchitecture.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CleanArchitecture.Infrastructure.Authentication;

internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            ClaimsPrincipal? principal = accessor.HttpContext?.User;

            string? id = principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(id, out Guid userId) ? userId : null;
        }
    }
}
