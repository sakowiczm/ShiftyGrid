using System.Text.Json;
using System.Text.Json.Serialization;

// todo: analyze build warnings

namespace ShiftyGrid.Server
{
    public class RequestConverter : JsonConverter<Request>
    {
        public override Request? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Parse the whole JSON into a JsonDocument so we can peek the type.
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProp))
                throw new JsonException("Missing type discriminator for Request.");

            var typeName = typeProp.GetString();
            if (string.IsNullOrEmpty(typeName))
                throw new JsonException("Empty type discriminator for Request.");

            var targetType = Type.GetType(typeName, throwOnError: true);
            // Deserialize the whole JSON into the concrete type.
            return (Request)JsonSerializer.Deserialize(root.GetRawText(), targetType, options)!;
        }

        public override void Write(Utf8JsonWriter writer, Request value, JsonSerializerOptions options)
        {
            // Capture the runtime type.
            var concreteType = value.GetType();
            // Write a discriminator.
            writer.WriteStartObject();
            writer.WriteString("type", concreteType.AssemblyQualifiedName!);

            // Write all other properties of the request.
            // We can serialize the object normally into a JsonDocument and then copy properties.
            var json = JsonSerializer.Serialize(value, concreteType, options);
            using var doc = JsonDocument.Parse(json);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name != "type") // skip the discriminator already written
                {
                    prop.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }
    }
}
