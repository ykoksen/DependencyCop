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
        /// Processes via <see cref="Account.Response.Process"/>.
        /// See also <see cref="Account.Response"/>.
        /// </summary>
        /// <param name="input">A <see cref="Account.Response"/> to process.</param>
        public void Execute(Account.Response input)
        {
            input.Process();
        }
    }
}
