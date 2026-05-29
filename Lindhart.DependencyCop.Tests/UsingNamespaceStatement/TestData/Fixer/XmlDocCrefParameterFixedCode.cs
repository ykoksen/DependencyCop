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
        /// See <see cref="Execute(Account.Response)"/>.
        /// </summary>
        public void Execute(Account.Response input)
        {
        }
    }
}
