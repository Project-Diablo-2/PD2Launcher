namespace PD2Shared.Utils
{
    public class DirectProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public DirectProgress(Action<T> handler)
        {
            _handler = handler;
        }

        void IProgress<T>.Report(T value)
        {
            this._handler(value);
        }
    }
}
