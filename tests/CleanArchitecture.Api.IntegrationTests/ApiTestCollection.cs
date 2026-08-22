using Xunit;

namespace CleanArchitecture.Api.IntegrationTests;

[CollectionDefinition(nameof(ApiTestCollection))]
public sealed class ApiTestCollection : ICollectionFixture<ApiTestFactory>;
