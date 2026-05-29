namespace UsingNamespaceStatementAnalyzer.Account
{
    class BaseEntity
    {
        public string Id { get; set; }
    }

    interface IRepository
    {
        void Save();
    }
}

namespace UsingNamespaceStatementAnalyzer.Transaction
{
    class Customer : Account.BaseEntity, Account.IRepository
    {
        public string Name { get; set; }

        public void Save()
        {
        }
    }
}
