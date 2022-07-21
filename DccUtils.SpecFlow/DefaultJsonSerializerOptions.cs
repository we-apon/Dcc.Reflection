using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dcc.SpecFlow;

internal static class DefaultJsonSerializerOptions {
    public static JsonSerializerOptions Instance {
        get {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            return options;
        }
    }
}
