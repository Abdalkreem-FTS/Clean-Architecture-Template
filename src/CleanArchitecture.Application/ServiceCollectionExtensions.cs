using CleanArchitecture.Application.Abstractions;
using CleanArchitecture.Application.Authentication;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Domain.Users;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IUserAccountService, UserAccountService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IUserProfileService, UserProfileService>();

        return services;
    }
}
