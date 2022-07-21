using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dcc.Extensions;
using Dcc.Reflection.TypeFormatting;
using Dcc.Reflection.TypeResolver;
using FluentAssertions;
using FluentAssertions.Primitives;
using TechTalk.SpecFlow;

namespace Dcc.SpecFlow;

public static class ObjectAssertionsExtensions {

    public static void BeAsJson(this ObjectAssertions assertions, string json, JsonSerializerOptions? serializerOptions = null, string because = "") {
        serializerOptions ??= new(JsonSerializerDefaults.Web);
        var prettyOptions = serializerOptions.WriteIndented ? serializerOptions : new(serializerOptions) {WriteIndented = true};

        var document = JsonDocument.Parse(json);
        assertions.NotBeNull($"it should be as json: {JsonSerializer.Serialize(document, prettyOptions)}");

        var expected = JsonSerializer.Deserialize(json, assertions.Subject.GetType(), serializerOptions);
        var expectedJson = JsonSerializer.Serialize(expected, serializerOptions);
        var actualJson = JsonSerializer.Serialize(assertions.Subject, serializerOptions);

        actualJson.Should().Be(expectedJson, because);
    }

    public static void BeAsTable(this ObjectAssertions assertions, Table table, JsonSerializerOptions? serializerOptions = null, Func<PropertyInfo, Type>? getPropertyInstanceType = null) {
        assertions.BeAsTable(assertions.Subject.GetType(), table, serializerOptions, getPropertyInstanceType);
    }

    public static void BeAsTable(this ObjectAssertions assertions, string typeName, Table table, JsonSerializerOptions? serializerOptions = null, Func<PropertyInfo, Type>? getPropertyInstanceType = null) {
        var type = TypeResolver.Resolve(typeName);
        type.Should().NotBeNull($"Тип {typeName} не удалось получить используя {nameof(TypeResolver)}");
        assertions.BeAsTable(type!, table, serializerOptions, getPropertyInstanceType);
    }

    public static void BeAsTable(this ObjectAssertions assertions, Type type, Table table, JsonSerializerOptions? serializerOptions = null, Func<PropertyInfo, Type>? getPropertyInstanceType = null, bool skipUnexpectedJsonElementFields = true) {
        assertions.NotBeNull();
        serializerOptions ??= DefaultJsonSerializerOptions.Instance;

        var expectedResult = table.To(type, serializerOptions, getPropertyInstanceType, ignoreReadOnlyProps:true);

        if (table.ContainsSingleJsonValue()) {
            assertions.BeAsJson(table.Rows[0][0], serializerOptions, $"produced json for {type.GetNestedName()} should equals to specified json. Object value is {assertions.Subject}");
            return;
        }

        foreach (var expected in table.FlipHorizontalTableToVertical(type).AsNameValueCollection(x => x[0], x => x[1])) {
            var value = GetPropertyValue(assertions.Subject, expected.Key, type);
            var expectedValue = GetPropertyValue(expectedResult, expected.Key, type);

            var isReadOnlyProperty = !assertions.Subject.GetPropertyPath(expected.Key).Last().CanWrite;
            if (!isReadOnlyProperty) {
                AssertEqual(value, expectedValue, skipUnexpectedJsonElementFields, expected.Key);
            }
            else {
                if (string.IsNullOrWhiteSpace(expected.Value) || expected.Value.Trim().Equals("null", StringComparison.InvariantCultureIgnoreCase)) {
                    value.Should().BeNull();
                }
                else {
                    AssertEqual(value!.ToString(), expected.Value, skipUnexpectedJsonElementFields, expected.Key);
                }
            }
        }

        static object? GetPropertyValue(object item, string propertyName, Type type) {
            object? value;
            try {
                value = item.GetPropertyValue(propertyName);
            }
            catch (Exception e) {
                throw new InvalidOperationException($"Can't get value of property {propertyName} on object of type {type.GetNestedName()}", e);
            }
            return value;
        }

    }

    static void AssertEqual(object? value, object? expectedValue, bool skipUnexpectedJsonElementFields, string propName) {
        if (value is JsonElement element && expectedValue is JsonElement expectedElement) {
            if (!skipUnexpectedJsonElementFields || element.ValueKind != JsonValueKind.Object || expectedElement.ValueKind != JsonValueKind.Object) {
                element.ToString().Should().Be(expectedElement.ToString(), $"Property {propName}");
                return;
            }

            var elementFields = element.EnumerateObject().ToDictionary(x => x.Name, x => x.Value);
            foreach (var expectedField in expectedElement.EnumerateObject()) {
                elementFields.TryGetValue(expectedField.Name, out var field).Should().BeTrue();
                AssertEqual(field, expectedField.Value, skipUnexpectedJsonElementFields, $"{propName}.{expectedField.Name}");
            }
            return;
        }

        if (value is JsonArray array && expectedValue is JsonArray expectedArray) {
            array.ToString().Should().Be(expectedArray.ToString(), $"Property {propName}");
            return;
        }


        if (value is DateTime date && expectedValue is DateTime expectedDate) {
            var utcDate = date.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : date.ToUniversalTime();
            var expectedUtcDate = expectedDate.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(expectedDate, DateTimeKind.Utc) : expectedDate.ToUniversalTime();
            utcDate.Should().Be(expectedUtcDate);
            return;
        }

        value.Should().BeEquivalentTo(expectedValue, $"Property {propName}");
    }
}
