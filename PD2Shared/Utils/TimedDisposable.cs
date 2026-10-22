using System.Diagnostics;

namespace PD2Shared.Utils
{
    public class TimedDisposable : IDisposable
    {
        private bool _disposed = false;

        private readonly Stopwatch _stopwatch;
        private readonly Action<TimeSpan> _onDisposeAction;

        public TimedDisposable(Action onConstructAction, Action<TimeSpan> onDisposeAction)
        {
            _onDisposeAction = onDisposeAction;
            _stopwatch = Stopwatch.StartNew();

            onConstructAction();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _onDisposeAction(_stopwatch.Elapsed);
            }

            _disposed = true;
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
