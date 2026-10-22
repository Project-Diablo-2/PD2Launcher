namespace PD2Launcherv2.Utils
{
    class ProgressWithCookie<T> : IProgress<T>
    {
        private readonly ProgressCookie _cookieRef;
        private readonly ProgressCookie _cookieCopy;

        private readonly Action<T> _handler;
        private readonly Progress<T> _progress;
        private readonly IProgress<T> _progressAsIProgress;

        public ProgressWithCookie(ProgressCookie cookie, Action<T> handler)
        {
            ArgumentNullException.ThrowIfNull(cookie, nameof(cookie));
            ArgumentNullException.ThrowIfNull(handler, nameof(handler));

            _cookieRef = cookie;
            _cookieCopy = new ProgressCookie(cookie);

            _handler = handler;
            _progress = new Progress<T>(HandlerWrapper);
            _progressAsIProgress = (IProgress<T>)_progress;
        }

        void IProgress<T>.Report(T value)
        {
            _progressAsIProgress.Report(value);
        }

        public event EventHandler<T>? ProgressChanged
        {
            add
            {
                _progress.ProgressChanged += value;
            }
            remove
            {
                _progress.ProgressChanged -= value;
            }
        }

        private void HandlerWrapper(T value)
        {
            if (_cookieRef != _cookieCopy)
            {
                return;
            }

            _handler(value);
        }
    }

    class ProgressCookie
    {
        private ulong _value;

        public ProgressCookie()
        {
        }

        public ProgressCookie(ProgressCookie other)
        {
            _value = other._value;
        }

        public void Advance()
        {
            ++_value;
        }

        public static bool operator ==(ProgressCookie? left, ProgressCookie? right)
        {
            if (left is null && right is null) return true;
            if (left is null || right is null) return false;

            return left._value == right._value;
        }

        public static bool operator !=(ProgressCookie? left, ProgressCookie? right)
        {
            return !(left == right);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (ReferenceEquals(obj, null))
            {
                return false;
            }

            var asProgressCookie = obj as ProgressCookie;

            if (asProgressCookie == null)
            {
                return false;
            }

            return this == asProgressCookie;
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }
    }
}
