using UsingNamespaceStatementAnalyzer.Account;

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
    class Customer : BaseEntity, IRepository
    {
        public string Name { get; set; }

        public void Save()
        {
        }
    }
}
