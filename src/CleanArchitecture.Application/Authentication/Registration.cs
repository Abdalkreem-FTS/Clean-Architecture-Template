namespace CleanArchitecture.Application.Authentication;

public sealed record Registration(string Email, string Password, string FirstName, string LastName);
