using System.Diagnostics;

namespace PD2Shared.Utils
{
    // A simplistic timer class that runs an uninterruptible action sequentially, on a dedicated thread, with a given interval until Disposed.
    //
    // It's meant to perform quick actions, such as periodic UI updates, hence the default AboveNormal ThreadPriority.
    public class SimpleTimer : IDisposable
    {
        private bool _disposed = false;

        private readonly EventWaitHandle _interrupt;

        private readonly TimeSpan _interval;
        private readonly Action _handler;
        private readonly Thread _thread;

        public SimpleTimer(TimeSpan interval, Action handler, ThreadPriority priority = ThreadPriority.AboveNormal)
        {
            if (interval < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(interval), $"{nameof(interval)} must not be negative");
            }

            ArgumentNullException.ThrowIfNull(handler, nameof(handler));

            _interrupt = new(initialState: false, EventResetMode.ManualReset);

            _interval = interval;

            _handler = handler;

            _thread = new Thread(ThreadEntry)
            {
                Priority = priority
            };
            _thread.Start();
        }

        public SimpleTimer(Action handler, ThreadPriority priority = ThreadPriority.AboveNormal)
            : this(interval: TimeSpan.FromMilliseconds(UpdateThrottle.DefaultIntervalMilliseconds), handler, priority)
        {
        }

        private void ThreadEntry()
        {
            Stopwatch stopwatch = new();
            TimeSpan timeToWait;

            while (true)
            {
                stopwatch.Restart();

                _handler();

                timeToWait = _interval - stopwatch.Elapsed;

                if (timeToWait > TimeSpan.Zero)
                {
                    if (_interrupt.WaitOne(timeToWait))
                    {
                        return;
                    }
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _interrupt.Set();
                _thread.Join();

                _interrupt.Dispose();
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
