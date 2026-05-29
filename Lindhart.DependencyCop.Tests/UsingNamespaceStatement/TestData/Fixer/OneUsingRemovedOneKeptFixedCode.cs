using System.Collections.Generic;

namespace UsingNamespaceStatementAnalyzer.Account
{
    class Item
    {
        public string Name { get; set; }
    }
}

namespace UsingNamespaceStatementAnalyzer.Transaction
{
    class MyClass
    {
        public List<Account.Item> GetItems() => new List<Account.Item>();
    }
}
