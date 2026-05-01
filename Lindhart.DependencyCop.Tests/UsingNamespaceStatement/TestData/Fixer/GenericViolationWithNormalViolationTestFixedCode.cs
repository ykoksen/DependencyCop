
namespace UsingNamespaceStatementAnalyzer.Account
{
    class Id<T>
    {
        public T Value { get; set; }
    }

    class Item
    {
        public string Name { get; set; }
    }
}

namespace UsingNamespaceStatementAnalyzer.Transaction
{
    class Current
    {
        public Account.Id<Account.Id<Account.Id<Account.Id<Account.Item>>>> AccountId { get; set; }

        public string Text { get; set; }
    }
}
