using CleanArchitecture.Domain.Common.Results;

namespace CleanArchitecture.Domain.Users;

public static class UserErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Users.NotFound", "No user was found with that identifier.");

    public static readonly Error EmailNotUnique =
        Error.Conflict("Users.EmailNotUnique", "That email address is already registered.");

    public static readonly Error InvalidCredentials =
        Error.Unauthorized("Users.InvalidCredentials", "Email address or password is incorrect.");

    public static readonly Error LockedOut =
        Error.Forbidden("Users.LockedOut", "Too many failed sign-in attempts. Try again later.");

    public static readonly Error EmailNotConfirmed =
        Error.Forbidden("Users.EmailNotConfirmed", "Confirm your email address before signing in.");

    public static readonly Error RoleNotFound =
        Error.NotFound("Users.RoleNotFound", "No such role exists.");

    public static readonly Error InvalidRefreshToken =
        Error.Unauthorized("Users.InvalidRefreshToken", "That refresh token is not valid.");

    public static Error IdentityFailure(string description) =>
        Error.Validation("Users.IdentityFailure", description);
}
