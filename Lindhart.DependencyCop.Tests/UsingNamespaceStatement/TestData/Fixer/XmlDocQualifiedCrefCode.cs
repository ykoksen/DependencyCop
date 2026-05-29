using UsingNamespaceStatementAnalyzer.Account;

namespace UsingNamespaceStatementAnalyzer.Account
{
    class Response
    {
        public string Value { get; set; }

        public void Process() { }
    }
}

namespace UsingNamespaceStatementAnalyzer.Transaction
{
    class MyClass
    {
        /// <summary>
        /// Processes via <see cref="Response.Process"/>.
        /// See also <see cref="Response"/>.
        /// </summary>
        /// <param name="input">A <see cref="Response"/> to process.</param>
        public void Execute(Response input)
        {
            input.Process();
        }
    }
}
