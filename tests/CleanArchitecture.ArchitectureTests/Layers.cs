using System.Reflection;
using CleanArchitecture.Application.Abstractions;
using CleanArchitecture.Domain.Common;
using CleanArchitecture.Infrastructure.Identity;

namespace CleanArchitecture.ArchitectureTests;

internal static class Layers
{
    public static readonly Assembly Domain = typeof(Ids).Assembly;

    public static readonly Assembly Application = typeof(IIdentityService).Assembly;

    public static readonly Assembly Infrastructure = typeof(ApplicationUser).Assembly;

    public static readonly Assembly Api = typeof(Program).Assembly;

    public const string DomainNamespace = "CleanArchitecture.Domain";
    public const string ApplicationNamespace = "CleanArchitecture.Application";
    public const string InfrastructureNamespace = "CleanArchitecture.Infrastructure";
    public const string ApiNamespace = "CleanArchitecture.Api";
}
