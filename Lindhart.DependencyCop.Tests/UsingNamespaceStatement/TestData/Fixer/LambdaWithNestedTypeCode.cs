using UsingNamespaceStatementAnalyzer.Account;

namespace UsingNamespaceStatementAnalyzer.Account
{
    public class Event
    {
        public class ResponseAfterManuallyHandled
        {
            public static ResponseAfterManuallyHandled Create(int x, int y) => new ResponseAfterManuallyHandled();
        }
    }

    class Item
    {
        public int Value { get; set; }
    }
}

namespace UsingNamespaceStatementAnalyzer.Transaction
{
    class MyClass
    {
        void MyMethod(Item[] items)
        {
            var result = ToList(items, x => Event.ResponseAfterManuallyHandled.Create(x.Value, 1));
        }

        T[] ToList<T, U>(U[] source, System.Func<U, T> selector) => null;
    }
}
