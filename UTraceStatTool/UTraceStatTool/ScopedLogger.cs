using System.Diagnostics;

namespace UTraceStatTool
{
    internal class ScopedLogger : IDisposable
    {
        public ScopedLogger(string message)
        {
            _message = message;
            Console.WriteLine($"{_message}...");
            _watch = System.Diagnostics.Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _watch.Stop();
            Console.WriteLine($"{_message} finished in {_watch.Elapsed}");
        }
        
        private readonly string _message;
        private readonly Stopwatch _watch;
    }
}
