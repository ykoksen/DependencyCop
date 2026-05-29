using UsingNamespaceStatementAnalyzer.Account;

namespace UsingNamespaceStatementAnalyzer.Account
{
    class Response
    {
        public string Value { get; set; }
    }
}

namespace UsingNamespaceStatementAnalyzer.Transaction
{
    class MyClass
    {
        /// <summary>
        /// Returns a <see cref="Response"/>.
        /// </summary>
        public Response GetResponse() => new Response();
    }
}
