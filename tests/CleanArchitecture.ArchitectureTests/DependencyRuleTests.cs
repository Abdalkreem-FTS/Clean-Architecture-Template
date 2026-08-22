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
