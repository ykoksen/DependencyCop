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
        /// See <see cref="Execute(Response)"/>.
        /// </summary>
        public void Execute(Response input)
        {
        }
    }
}
