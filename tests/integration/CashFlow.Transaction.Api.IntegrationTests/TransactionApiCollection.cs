using Xunit;

namespace CashFlow.Transaction.Api.IntegrationTests;

[CollectionDefinition("TransactionApi")]
public class TransactionApiCollection : ICollectionFixture<TransactionApiFactory>;
