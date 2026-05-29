using UsingNamespaceStatementAnalyzer.Account;

namespace UsingNamespaceStatementAnalyzer.Account
{
    class Response
    {
        public string Property { get; set; }
    }

    class Container
    {
        public Response InnerResponse { get; set; }
    }
}

namespace UsingNamespaceStatementAnalyzer.Transaction
{
    class MyClass
    {
        void MyMethod(Container container)
        {
            var value = container.InnerResponse;
        }
    }
}
