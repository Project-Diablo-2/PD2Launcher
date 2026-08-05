using Org.BouncyCastle.Crypto.Digests;
using System.IO.Hashing;
using System.Security.Cryptography;

namespace PD2Shared.GameFileUpdate.Internal
{
    internal abstract class Digest
    {
        public static NonFinalizingDigest GetNonFinalizing(Hash hash)
        {
            if (hash is Md5Hash)
            {
                return new NonFinalizingMd5();
            }
            else if (hash is Xxh3Hash)
            {
                return new NonFinalizingXxh3();
            }
            else
            {
                throw new NotSupportedException($"Unable to create {nameof(NonFinalizingDigest)} for {nameof(Hash)} of type {hash.GetType()}.");
            }
        }

        public static DisposableDigest GetDisposable(Hash hash)
        {
            if (hash is Md5Hash)
            {
                return new DisposableMd5();
            }
            else if (hash is Xxh3Hash)
            {
                return new DisposableXxh3();
            }
            else
            {
                throw new NotSupportedException($"Unable to create {nameof(DisposableDigest)} for {nameof(Hash)} of type {hash.GetType()}.");
            }
        }

        private readonly bool _isFinalizing;
        private Hash _finalizedHash = null!;
        private bool _finalized = false;

        protected Digest() : this(isFinalizing: true)
        {
        }

        protected Digest(bool isFinalizing)
        {
            _isFinalizing = isFinalizing;
        }

        public abstract string HashName { get; }

        public abstract bool IsHashType<THash>() where THash : Hash;

        private void CheckIfFinalized()
        {
            if (_finalized)
            {
                throw new InvalidOperationException("Digest is finalized");
            }
        }

        public async Task<Hash> HashStream(Stream inputStream, CancellationToken cancellationToken)
        {
            CheckIfFinalized();

            if (_isFinalizing)
            {
                _finalized = true;
                _finalizedHash = await HashStreamInternal(inputStream, cancellationToken).ConfigureAwait(false);

                return _finalizedHash;
            }
            else
            {
                return await HashStreamInternal(inputStream, cancellationToken).ConfigureAwait(false);
            }
        }

        protected abstract Task<Hash> HashStreamInternal(Stream inputStream, CancellationToken cancellationToken);

        public void Update(byte[] buffer, int offset, int count)
        {
            CheckIfFinalized();

            UpdateInternal(buffer, offset, count);
        }

        protected abstract void UpdateInternal(byte[] buffer, int offset, int count);

        public Hash GetHash()
        {
            CheckIfFinalized();

            if (_isFinalizing)
            {
                _finalized = true;
                _finalizedHash = GetHashInternal();

                return _finalizedHash;
            }
            else
            {
                return GetHashInternal();
            }
        }

        protected abstract Hash GetHashInternal();
    }

    internal abstract class NonFinalizingDigest : Digest
    {
        protected NonFinalizingDigest() : base(isFinalizing: false)
        {
        }
    }

    internal abstract class DisposableDigest : Digest, IDisposable
    {
        public abstract void Dispose();
    }

    internal abstract class StrongNonFinalizingDigest<THash> : NonFinalizingDigest where THash : Hash, new()
    {
        private static readonly THash _emptyHash = new();

        public override string HashName => _emptyHash.Name;
        public override bool IsHashType<TOtherHash>()
        {
            return typeof(THash) == typeof(TOtherHash);
        }
    }

    internal abstract class StrongDisposableDigest<THash> : DisposableDigest where THash : Hash, new()
    {
        private static readonly THash _emptyHash = new();

        public override string HashName => _emptyHash.Name;
        public override bool IsHashType<TOtherHash>()
        {
            return typeof(THash) == typeof(TOtherHash);
        }
    }

    // Concrete classes:

    internal class DisposableMd5 : StrongDisposableDigest<Md5Hash>
    {
        private static readonly byte[] _emptyBuffer = Array.Empty<byte>();
        private readonly MD5 _md5 = MD5.Create();

        protected override async Task<Hash> HashStreamInternal(Stream inputStream, CancellationToken cancellationToken)
        {
            return new Md5Hash(await _md5.ComputeHashAsync(inputStream, cancellationToken).ConfigureAwait(false));
        }

        protected override void UpdateInternal(byte[] buffer, int offset, int count)
        {
            _md5.TransformBlock(buffer, offset, count, outputBuffer: null, outputOffset: 0);
        }

        protected override Md5Hash GetHashInternal()
        {
            _md5.TransformFinalBlock(_emptyBuffer, inputOffset: 0, inputCount: 0);

            return new Md5Hash(_md5.Hash!);
        }

        public override void Dispose()
        {
            ((IDisposable)_md5).Dispose();
        }
    }

    // This is merely a thin wrapper around NonFinalizingXxh3 to provide a DisposableDigest counterpart to DisposableMd5 so the two could be used interchangeably
    internal class DisposableXxh3 : StrongDisposableDigest<Xxh3Hash>
    {
        private readonly NonFinalizingXxh3 _xxh3 = new();

        protected override async Task<Hash> HashStreamInternal(Stream inputStream, CancellationToken cancellationToken)
        {
            return (Xxh3Hash)await _xxh3.HashStream(inputStream, cancellationToken).ConfigureAwait(false);
        }

        protected override void UpdateInternal(byte[] buffer, int offset, int count)
        {
            _xxh3.Update(buffer, offset, count);
        }

        protected override Xxh3Hash GetHashInternal()
        {
            return (Xxh3Hash)_xxh3.GetHash();
        }

        public override void Dispose()
        {
            // Nothing to do here
        }
    }

    internal class NonFinalizingMd5 : StrongNonFinalizingDigest<Md5Hash>
    {
        private readonly MD5Digest _md5 = new();

        protected override async Task<Hash> HashStreamInternal(Stream inputStream, CancellationToken cancellationToken)
        {
            // This won't be needed anyway

            throw new NotImplementedException();
        }

        protected override void UpdateInternal(byte[] buffer, int offset, int count)
        {
            _md5.BlockUpdate(buffer, offset, count);
        }

        protected override Md5Hash GetHashInternal()
        {
            var bytes = new byte[_md5.GetDigestSize()];

            new MD5Digest(_md5).DoFinal(bytes, outOff: 0);

            return new Md5Hash(bytes);
        }
    }

    internal class NonFinalizingXxh3 : StrongNonFinalizingDigest<Xxh3Hash>
    {
        private readonly XxHash3 _xxh3 = new();

        protected override async Task<Hash> HashStreamInternal(Stream inputStream, CancellationToken cancellationToken)
        {
            await _xxh3.AppendAsync(inputStream, cancellationToken).ConfigureAwait(false);
            return new Xxh3Hash(_xxh3.GetCurrentHash());
        }

        protected override void UpdateInternal(byte[] buffer, int offset, int count)
        {
            _xxh3.Append(new ReadOnlySpan<byte>(buffer, offset, count));
        }

        protected override Xxh3Hash GetHashInternal()
        {
            return new Xxh3Hash(_xxh3.GetCurrentHash());
        }
    }
}
