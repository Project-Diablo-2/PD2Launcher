namespace PD2Shared.GameFileUpdate
{
    public abstract class Hash
    {
        protected Hash()
        {
            this.Bytes = Array.Empty<byte>();
        }

        protected Hash(byte[] bytes)
        {
            if (bytes.Length != this.SizeInBytes)
            {
                throw new ArgumentException($"Invalid hash size for {this.SizeInBytes * 8} bit {this.Name}: {this.SizeInBytes} != {bytes.Length} bytes (actual)", nameof(bytes));
            }

            this.Bytes = bytes;
        }

        protected Hash(string hexString)
        {
            ArgumentNullException.ThrowIfNull(hexString, nameof(hexString));

            byte[] bytes;

            try
            {
                bytes = Convert.FromHexString(hexString);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException($"Not a valid hash: '{hexString}'", nameof(hexString), ex);
            }

            if (bytes.Length != this.SizeInBytes)
            {
                throw new ArgumentException($"Not a valid {this.SizeInBytes * 8} bit {this.Name}: '{hexString}'", nameof(hexString));
            }

            this.Bytes = bytes;
        }

        public string ToHexString()
        {
            return Convert.ToHexString(Bytes).ToLowerInvariant();
        }

        public abstract string Name { get; }
        public abstract int SizeInBytes { get; }

        public byte[] Bytes { get; }

        public static bool operator ==(Hash? left, Hash? right)
        {
            if (left is null && right is null) return true;
            if (left is null || right is null) return false;

            if (left.SizeInBytes != right.SizeInBytes)
            {
                return false;
            }

            if (left.Name != right.Name)
            {
                return false;
            }

            return left.Bytes.SequenceEqual(right.Bytes);
        }
        public static bool operator !=(Hash? left, Hash? right)
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

            var objAsHash = obj as Hash;

            if (objAsHash is null)
            {
                return false;
            }

            return this == objAsHash;
        }

        public override int GetHashCode()
        {
            return this.Bytes.GetHashCode();
        }
    }

    // ...and all the available concrete classes

    public class Md5Hash : Hash
    {
        public Md5Hash() : base() { }

        public Md5Hash(byte[] bytes) : base(bytes) { }
        public Md5Hash(string hexString) : base(hexString) { }

        public override string Name { get => "MD5"; }
        public override int SizeInBytes { get => 128 / 8; }
    }

    public class Xxh3Hash : Hash
    {
        public Xxh3Hash() : base() { }

        public Xxh3Hash(byte[] bytes) : base(bytes) { }
        public Xxh3Hash(string hexString) : base(hexString) { }

        public override string Name { get => "XXH3"; }
        public override int SizeInBytes { get => 64 / 8; }
    }
}
