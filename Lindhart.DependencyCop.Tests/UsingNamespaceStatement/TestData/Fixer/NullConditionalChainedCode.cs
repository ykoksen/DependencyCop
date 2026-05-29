using UsingNamespaceStatementAnalyzer.Account;

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
        void MyMethod<T>(Envelope<T>? responseEnvelope) where T : class, IBase
        {
            var currentAddress = responseEnvelope?.ProcessInformation?.Address;
        }
    }
}
