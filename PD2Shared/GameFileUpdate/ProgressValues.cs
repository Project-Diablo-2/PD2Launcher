using PD2Shared.GameFileUpdate.Internal;

namespace PD2Shared.GameFileUpdate
{
    public partial class ProgressValues
    {
        public class FileCountProgress
        {
            public int Current { get; set; }
            public int Total { get; set; }
        }

        public class BytesProgress
        {
            public long Current { get; set; }
            public long? Total { get; set; }
        }

        public class BytesPerSecProgress
        {
            public long Bytes { get; set; }
            public long ElapsedMilliseconds { get; set; }
        }

        private Data _data = new();

        public ProgressValues SetTotal(double total)
        {
            CheckIfExtracted();

            if (!double.IsFinite(total))
            {
                throw new ArithmeticException($"'{nameof(total)}' must be finite");
            }

            _data.Total = total;
            _data.TotalSet = true;
            return this;
        }

        public ProgressValues SetTotal(double current, double total)
        {
            CheckIfExtracted();

            if (!double.IsFinite(current))
            {
                throw new ArithmeticException($"'{nameof(current)}' must be finite");
            }

            if (!double.IsFinite(total))
            {
                throw new ArithmeticException($"'{nameof(total)}' must be finite");
            }

            if (total == 0)
            {
                throw new ArithmeticException($"'{nameof(total)}' must not be 0");
            }

            _data.Total = (double)current / total;
            _data.TotalSet = true;
            return this;
        }

        public ProgressValues ClearTotal()
        {
            CheckIfExtracted();

            _data.Total = null;
            _data.TotalSet = true;
            return this;
        }

        public ProgressValues SetFileCount(int current, int total)
        {
            CheckIfExtracted();

            _data.FileCount = new FileCountProgress { Current = current, Total = total };
            _data.FileCountSet = true;
            return this;
        }

        public ProgressValues ClearFileCount()
        {
            CheckIfExtracted();

            _data.FileCount = null;
            _data.FileCountSet = true;
            return this;
        }

        public ProgressValues SetBytes(long current, long? total)
        {
            CheckIfExtracted();

            _data.Bytes = new BytesProgress { Current = current, Total = total };
            _data.BytesSet = true;
            return this;
        }

        public ProgressValues SetBytes(long current, long total)
        {
            CheckIfExtracted();

            _data.Bytes = new BytesProgress { Current = current, Total = total.IsInvalidSize() ? null : total };
            _data.BytesSet = true;
            return this;
        }

        public ProgressValues SetBytes(long current)
        {
            CheckIfExtracted();

            _data.Bytes = new BytesProgress { Current = current, Total = null };
            _data.BytesSet = true;
            return this;
        }

        public ProgressValues ClearBytes()
        {
            CheckIfExtracted();

            _data.Bytes = null;
            _data.BytesSet = true;
            return this;
        }

        public ProgressValues SetBytesPerSec(long bytes, long elapsedMilliseconds)
        {
            CheckIfExtracted();

            if (elapsedMilliseconds == 0)
            {
                throw new ArithmeticException($"'{nameof(elapsedMilliseconds)}' must not be 0");
            }

            _data.BytesPerSec = new BytesPerSecProgress { Bytes = bytes, ElapsedMilliseconds = elapsedMilliseconds };
            _data.BytesPerSecSet = true;
            return this;
        }

        public ProgressValues ClearBytesPerSec()
        {
            CheckIfExtracted();

            _data.BytesPerSec = null;
            _data.BytesPerSecSet = true;
            return this;
        }

        public ProgressValues Clear()
        {
            CheckIfExtracted();

            this.ClearTotal();
            this.ClearFileCount();
            this.ClearBytes();
            this.ClearBytesPerSec();
            return this;
        }

        private void CheckIfExtracted()
        {
            if (_data == null)
            {
                throw new InvalidOperationException($"{nameof(ProgressValues)} already extracted");
            }
        }

        public IData Extract()
        {
            CheckIfExtracted();

            var res = _data;
            _data = null!;
            return res;
        }
    }
}
