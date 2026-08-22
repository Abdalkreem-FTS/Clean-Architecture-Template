using CleanArchitecture.Application.Authentication;
using CleanArchitecture.Domain.Common.Results;

namespace CleanArchitecture.Application.Abstractions;

public interface IAuthenticationService
{
    Task<Result<Guid>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<Result<AuthenticationResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<Result<AuthenticationResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken);

    Task<Result<Success>> LogoutAsync(RefreshRequest request, CancellationToken cancellationToken);
}
