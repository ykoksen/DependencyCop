using UsingNamespaceStatementAnalyzer.Account;

namespace UsingNamespaceStatementAnalyzer.Account
{
    class Item
    {
        public string Name { get; set; }
    }
}

namespace UsingNamespaceStatementAnalyzer.Transaction
{
    class Id<T>
    {
        public T Value { get; set; }
    }

    class Current
    {
        public Id<Item> AccountId { get; set; }

        public string Text { get; set; }
    }
}
