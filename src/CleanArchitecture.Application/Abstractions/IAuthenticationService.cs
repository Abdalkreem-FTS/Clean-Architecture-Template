using CleanArchitecture.Application.Authentication;
using CleanArchitecture.Domain.Common.Results;

namespace CleanArchitecture.Application.Abstractions;

public interface IAuthenticationService
{
    Task<Result<Guid>> RegisterAsync(Registration registration, CancellationToken cancellationToken);

    Task<Result<AuthenticationTokens>> LoginAsync(string email, string password, CancellationToken cancellationToken);

    Task<Result<AuthenticationTokens>> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    Task<Result<Success>> LogoutAsync(string refreshToken, CancellationToken cancellationToken);
}
