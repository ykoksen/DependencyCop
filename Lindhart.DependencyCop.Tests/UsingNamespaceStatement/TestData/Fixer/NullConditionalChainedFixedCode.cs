
namespace UsingNamespaceStatementAnalyzer.Account
{
    class ProcessInfo
    {
        public string Address { get; set; }
    }

    class Envelope<T>
    {
        public ProcessInfo ProcessInformation { get; set; }
        public T PayloadResponse { get; set; }
    }

    interface IBase { }
}

namespace UsingNamespaceStatementAnalyzer.Transaction
{
    class MyClass
    {
        void MyMethod<T>(Account.Envelope<T>? responseEnvelope) where T : class, Account.IBase
        {
            var currentAddress = responseEnvelope?.ProcessInformation?.Address;
        }
    }
}
