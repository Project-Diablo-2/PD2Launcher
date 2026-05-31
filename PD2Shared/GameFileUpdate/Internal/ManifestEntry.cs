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

        public ManifestEntry(string path, string md5, long? size = null, string? xxh3 = null)
        {
            this.Path = path;
            this.LooseValidation = LooseValidationFileSet.Contains(this.Path);
            this.Md5Hash = new Md5Hash(md5);

            // These implicitly set Dirty = true

            this.Size = size;
            this.Xxh3Hash = xxh3 == null ? (Xxh3Hash?)null : new Xxh3Hash(xxh3);
        }

        public ManifestEntry(string path, SerializableManifest.Entry entry) : this(path, entry.Md5, entry.Size, entry.Xxh3)
        {
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

        public Md5Hash Md5Hash { get; }

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

        private Xxh3Hash? _xxh3Hash = null;
        public Xxh3Hash? Xxh3Hash
        {
            get => _xxh3Hash;
            set
            {
                if (_xxh3Hash == value)
                {
                    return;
                }

                _xxh3Hash = value;
                Dirty = true;
            }
        }

        public Hash BestHash
        {
            get => Xxh3Hash != null ? Xxh3Hash : Md5Hash;
        }

        public bool Dirty { get; set; }

        public SerializableManifest.Entry ToSerializable()
        {
            return new SerializableManifest.Entry(this.Md5Hash.ToHexString(), this.Size, this.Xxh3Hash?.ToHexString());
        }
    }
}
