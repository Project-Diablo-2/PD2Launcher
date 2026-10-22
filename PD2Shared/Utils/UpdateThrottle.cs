using System.Diagnostics;

namespace PD2Shared.Utils
{
    // Beware, this class isn't thread-safe
    public class UpdateThrottle
    {
        public const int DefaultIntervalMilliseconds = 50;

        private readonly Stopwatch _stopwatch;
        private readonly int _interval;

        public UpdateThrottle(int intervalMilliseconds = DefaultIntervalMilliseconds, bool waitInitialInterval = false)
        {
            _stopwatch = new Stopwatch();
            _interval = intervalMilliseconds;

            if (waitInitialInterval)
            {
                _stopwatch.Start();
            }
        }

        public void Reset()
        {
            _stopwatch.Restart();
        }

        private bool CanUpdateInternal(out long elapsed)
        {
            if (!_stopwatch.IsRunning)
            {
                _stopwatch.Start();

                elapsed = _interval;
                return true;
            }

            elapsed = _stopwatch.ElapsedMilliseconds;
            return elapsed >= _interval;
        }

        public void UpdateIfPossible(Action<long> action)
        {
            if (CanUpdateInternal(out long elapsed))
            {
                Reset();
            }
            else
            {
                return;
            }

            action(elapsed);
        }

        public void UpdateIfPossible(Action action)
        {
            UpdateIfPossible((_) => action());
        }
    }
}
