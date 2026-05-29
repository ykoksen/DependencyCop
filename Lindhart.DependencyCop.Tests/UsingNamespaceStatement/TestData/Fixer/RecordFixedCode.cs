
namespace UsingNamespaceStatementAnalyzer.Account
{
    class Response
    {
        public string Value { get; set; }
    }
}

namespace UsingNamespaceStatementAnalyzer.Transaction
{
    record CustomerRecord
    {
        public Account.Response GetResponse() => new Account.Response();
    }
}
