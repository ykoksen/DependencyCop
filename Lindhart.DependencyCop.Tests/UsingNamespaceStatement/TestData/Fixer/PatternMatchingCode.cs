using UsingNamespaceStatementAnalyzer.Account;

namespace UsingNamespaceStatementAnalyzer.Account
{
    class Entity
    {
        public string Id { get; set; }
    }

    class PremiumEntity : Entity
    {
        public string PremiumId { get; set; }
    }
}

namespace UsingNamespaceStatementAnalyzer.Transaction
{
    class Processor
    {
        void Process(object obj)
        {
            var asEntity = obj as Entity;

            if (obj is PremiumEntity premium)
            {
                _ = premium.PremiumId;
            }
        }
    }
}
