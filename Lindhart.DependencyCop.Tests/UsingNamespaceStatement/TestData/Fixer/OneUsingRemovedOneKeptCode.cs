using UsingNamespaceStatementAnalyzer.Account;
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
        public List<Item> GetItems() => new List<Item>();
    }
}
