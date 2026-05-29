
namespace UsingNamespaceStatementAnalyzer.Account
{
    enum TransportIdPurpose
    {
        NormalFlow,
        Retry
    }

    class SuccessfullySentToLink
    {
        public SuccessfullySentToLink(string address) { }
    }
}

namespace UsingNamespaceStatementAnalyzer.Transaction
{
    class MyClass
    {
        object[] GetEvents(string address)
        {
            return new object[] { new Account.SuccessfullySentToLink(address), Account.TransportIdPurpose.NormalFlow };
        }
    }
}
