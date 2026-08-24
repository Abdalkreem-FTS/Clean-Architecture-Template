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
            // The bearer handler is configured with MapInboundClaims off, so claims arrive under
            // the names the token was issued with. "sub" is the one and only place the id lives.
            string? id = accessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            return Guid.TryParse(id, out Guid userId) ? userId : null;
        }
    }
}
