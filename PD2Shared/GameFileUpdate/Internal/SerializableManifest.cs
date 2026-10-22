using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace PD2Shared.GameFileUpdate.Internal
{
    internal class SerializableManifest
    {
        internal class Entry
        {
            [JsonConstructor]
            public Entry(string md5, long? size, string? xxh3)
            {
                Md5 = md5;
                Size = size;
                Xxh3 = xxh3;
            }

            public string Md5 { get; }
            public long? Size { get; }
            public string? Xxh3 { get; }
        }

        public SerializableManifest(ManifestEntry[] manifestEntries)
        {
            Entries = manifestEntries
                .ToImmutableSortedDictionary(e => e.Path, e => e.ToSerializable(), StringComparer.OrdinalIgnoreCase);
            Count = Entries.Count;
        }

        [JsonConstructor]
        public SerializableManifest(ImmutableSortedDictionary<string, Entry> entries, int count)
        {
            Entries = entries;
            Count = count;
        }

        public ImmutableSortedDictionary<string, Entry> Entries { get; }
        public int Count { get; }
    }
}
