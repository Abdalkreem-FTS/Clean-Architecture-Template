using System.Reflection;
using NetArchTest.Rules;
using TestResult = NetArchTest.Rules.TestResult;
using Shouldly;

namespace CleanArchitecture.ArchitectureTests;

// Dependencies point inwards only: Api -> Infrastructure -> Application -> Domain
public sealed class DependencyRuleTests
{
    [Fact]
    public void Domain_ShouldNotDependOn_AnyOtherLayer()
    {
        TestResult result = Types.InAssembly(Layers.Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                Layers.ApplicationNamespace,
                Layers.InfrastructureNamespace,
                Layers.ApiNamespace)
            .GetResult();

        result.ShouldBeSuccessful(
            "CleanArchitecture.Domain is the innermost layer and must not know any other exists.");
    }

    [Fact]
    public void Application_ShouldNotDependOn_InfrastructureOrApi()
    {
        TestResult result = Types.InAssembly(Layers.Application)
            .ShouldNot()
            .HaveDependencyOnAny(Layers.InfrastructureNamespace, Layers.ApiNamespace)
            .GetResult();

        result.ShouldBeSuccessful(
            "CleanArchitecture.Application declares ports; CleanArchitecture.Infrastructure implements them. The arrow points inwards only.");
    }

    [Fact]
    public void Infrastructure_ShouldNotDependOn_Api()
    {
        TestResult result = Types.InAssembly(Layers.Infrastructure)
            .ShouldNot()
            .HaveDependencyOn(Layers.ApiNamespace)
            .GetResult();

        result.ShouldBeSuccessful(
            "CleanArchitecture.Api is the composition root. Nothing references it.");
    }

    // Direction alone does not catch a mutable entity or a configuration someone made public.
    // These two rules cover the shapes the layering is meant to protect.
    [Fact]
    public void Domain_ShouldNotExpose_PublicSetters()
    {
        string[] offenders = [.. Layers.Domain
            .GetTypes()
            .Where(type => type.IsPublic)
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(property => property.SetMethod is { IsPublic: true })
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}")];

        offenders.ShouldBeEmpty(
            "A domain type that anyone can mutate cannot hold an invariant. Offenders: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void ApplicationPorts_ShouldNotExpose_WireContracts()
    {
        string[] offenders = [.. Layers.Application
            .GetTypes()
            .Where(type => type is { IsInterface: true, Namespace: $"{Layers.ApplicationNamespace}.Abstractions" })
            .SelectMany(type => type.GetMethods())
            .SelectMany(Signature)
            .Where(type => type.Name.Contains("Request", StringComparison.Ordinal)
                || type.Name.Contains("Response", StringComparison.Ordinal))
            .Select(type => type.Name)
            .Distinct()];

        offenders.ShouldBeEmpty(
            "A port speaks the application's own types, not the HTTP contract. Offenders: "
            + string.Join(", ", offenders));
    }

    private static IEnumerable<Type> Signature(MethodInfo method) =>
        Unwrap(method.ReturnType)
            .Concat(method.GetParameters().SelectMany(parameter => Unwrap(parameter.ParameterType)));

    private static IEnumerable<Type> Unwrap(Type type)
    {
        yield return type;

        if (!type.IsGenericType)
        {
            yield break;
        }

        foreach (Type nested in type.GetGenericArguments().SelectMany(Unwrap))
        {
            yield return nested;
        }
    }

    [Fact]
    public void EntityConfigurations_ShouldBe_InternalAndSealed()
    {
        string[] offenders = [.. Layers.Infrastructure
            .GetTypes()
            .Where(type => type.GetInterfaces()
                .Any(contract => contract.Name.StartsWith("IEntityTypeConfiguration", StringComparison.Ordinal)))
            .Where(type => type.IsPublic || !type.IsSealed)
            .Select(type => type.Name)];

        offenders.ShouldBeEmpty(
            "Entity configurations are wired up by assembly scan, so nothing outside Infrastructure "
            + "needs to see them. Offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Domain_ShouldReference_NothingButTheBaseClassLibrary()
    {
        string[] referenced = [.. Layers.Domain
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name!)
            .Where(name => !name.StartsWith("System", StringComparison.Ordinal)
                && !name.Equals("netstandard", StringComparison.Ordinal))];

        referenced.ShouldBeEmpty(
            $"CleanArchitecture.Domain must stay dependency-free but references: {string.Join(", ", referenced)}");
    }
}
