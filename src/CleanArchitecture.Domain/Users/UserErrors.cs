using CleanArchitecture.Domain.Common.Results;

namespace CleanArchitecture.Domain.Users;

public static class UserErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Users.NotFound", "No user was found with that identifier.");

    public static readonly Error EmailNotUnique =
        Error.Conflict("Users.EmailNotUnique", "That email address is already registered.");

    // Also returned for a locked-out account, deliberately: a distinct status here would let an
    // attacker tell which emails exist by brute-forcing until the response changes.
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("Users.InvalidCredentials", "Email address or password is incorrect.");

    public static readonly Error EmailNotConfirmed =
        Error.Forbidden("Users.EmailNotConfirmed", "Confirm your email address before signing in.");

    public static readonly Error RoleNotFound =
        Error.NotFound("Users.RoleNotFound", "No such role exists.");

    public static readonly Error InvalidRefreshToken =
        Error.Unauthorized("Users.InvalidRefreshToken", "That refresh token is not valid.");

    public static Error IdentityFailure(string code, string description) =>
        Error.Validation($"Users.{code}", description);
}
