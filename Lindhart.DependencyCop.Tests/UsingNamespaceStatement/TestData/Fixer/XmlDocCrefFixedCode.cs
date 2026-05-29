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
        /// Returns a <see cref="Account.Response"/>.
        /// </summary>
        public Account.Response GetResponse() => new Account.Response();
    }
}
