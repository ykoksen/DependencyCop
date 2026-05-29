namespace UsingNamespaceStatementAnalyzer.Account
{
    class Entity
    {
        public string Id { get; set; }
    }
}

namespace UsingNamespaceStatementAnalyzer.Transaction
{
    class Processor
    {
        System.Type GetEntityType() => typeof(Account.Entity);
    }
}
