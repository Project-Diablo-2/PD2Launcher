using System.Text.Json;
using System.Text.Json.Serialization;

namespace PD2Shared.GameFileUpdate
{
    internal class Xxh3HashJsonConverter : JsonConverter<Xxh3Hash>
    {
        public override Xxh3Hash Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new Xxh3Hash(reader.GetString()!);
        }

        public override void Write(Utf8JsonWriter writer, Xxh3Hash value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToHexString());
        }
    }
}
