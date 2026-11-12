using Xunit;

namespace CashFlow.Identity.Api.IntegrationTests;

[CollectionDefinition("IdentityApi")]
public class IdentityApiCollection : ICollectionFixture<IdentityApiFactory>;
