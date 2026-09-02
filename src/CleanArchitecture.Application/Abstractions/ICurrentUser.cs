namespace CleanArchitecture.Application.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }
}
