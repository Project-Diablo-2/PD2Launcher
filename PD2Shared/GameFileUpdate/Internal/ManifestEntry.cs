using System.Diagnostics.CodeAnalysis;

namespace PD2Shared.GameFileUpdate.Internal
{
    internal class ManifestEntry
    {
        // Set of files that are subject to "loose validation" (they need to merely exist and have a non-zero size) as InstallFiles.
        // Treat contained keys as exact paths. If any globbing is needed -- use a proper library for that.
        //
        // <!> The file list provided by Constants.excludedFiles might be too broad, but stick with it for now...
        private static readonly HashSet<string> LooseValidationFileSet = new(Constants.excludedFiles, StringComparer.OrdinalIgnoreCase);

        public ManifestEntry(string path, string md5, long? size = null)
        {
            this.Path = path;
            this.LooseValidation = LooseValidationFileSet.Contains(this.Path);
            this.Md5Bytes = Md5BytesFromHexString(md5);
            this.Md5 = md5;
            this.Size = size; // Implicitly sets Dirty = true
        }

        public ManifestEntry(string path, SerializableManifest.Entry entry) : this(path, entry.Md5, entry.Size)
        {
            if (!this.LooseValidation && this.Size == null)
            {
                throw new InvalidOperationException($"Cannot deserialize {nameof(ManifestEntry)} with {nameof(entry.Size)} == null and {nameof(LooseValidation)} == {this.LooseValidation}.");
            }

            this.Dirty = false;
        }

        private string _path;
        public string Path
        {
            get => _path;
            [MemberNotNull(nameof(_path))]
            init
            {
                ArgumentNullException.ThrowIfNull(value, nameof(value));

                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException($"Invalid empty path: '{value}'", nameof(value));
                }

                if (System.IO.Path.IsPathRooted(value))
                {
                    throw new ArgumentException($"The path is rooted: '{value}'", nameof(value));
                }

                _path = value;
            }
        }
        public bool LooseValidation { get; }
        public byte[] Md5Bytes { get; }

        private string Md5 { get; }

        private long? _size;
        public long? Size
        {
            get => _size;
            set
            {
                if (_size == value)
                {
                    return;
                }

                if (value != null && value <= 0)
                {
                    throw new ArgumentException($"'{nameof(Size)}' must be > 0", nameof(value));
                }

                _size = value;
                Dirty = true;
            }
        }

        public bool Dirty { get; set; }

        public SerializableManifest.Entry ToSerializable()
        {
            // Files that cannot be loosely validated need to have a Size
            if (!this.LooseValidation && this.Size == null)
            {
                throw new InvalidOperationException($"Cannot serialize {nameof(ManifestEntry)} with unspecified {nameof(Size)} when {nameof(LooseValidation)} is {this.LooseValidation}.");
            }

            return new SerializableManifest.Entry(this.Md5, this.Size);
        }

        private static byte[] Md5BytesFromHexString(string md5String)
        {
            ArgumentNullException.ThrowIfNull(md5String, nameof(md5String));

            byte[] res;

            try
            {
                res = Convert.FromHexString(md5String);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException($"Not a valid MD5 hash: '{md5String}'", nameof(md5String), ex);
            }

            // MD5 hashes are 128 bits long
            if (res.Length != (128 / 8))
            {
                throw new ArgumentException($"Not a valid MD5 hash: '{md5String}'", nameof(md5String));
            }

            return res;
        }
    }
}
