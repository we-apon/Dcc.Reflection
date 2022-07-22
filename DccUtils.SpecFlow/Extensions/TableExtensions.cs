using System.Collections;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dcc.Extensions;
using FluentAssertions;
using TechTalk.SpecFlow;
using TechTalk.SpecFlow.Assist;
using TechTalk.SpecFlow.Tracing;

namespace Dcc.SpecFlow;

public static class TableExtensions {

    public static ImmutableDictionary<string, TValue> ToDictionary<TValue>(this Table table, Func<TableRow, string> keySelector, Func<TableRow, TValue> valueSelector) {
        return table.AsNameValueCollection(keySelector, valueSelector).ToImmutableDictionary();
    }

    public static IEnumerable<KeyValuePair<string, TValue>> AsNameValueCollection<TValue>(this Table table, Func<TableRow, string> keySelector, Func<TableRow, TValue> valueSelector) {
        foreach (var row in table.Rows) {
            yield return new KeyValuePair<string, TValue>(keySelector.Invoke(row), valueSelector.Invoke(row));
        }
    }

    static readonly MethodInfo ToGenericMethodDefinition = typeof(TableExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(x => x.Name == "To" && x.IsGenericMethod);

    internal static readonly ImmutableDictionary<Type, MethodInfo> ParsedTypes = new Dictionary<Type, MethodInfo>() {
        {typeof(byte), GetParseMethod(typeof(byte))},
        {typeof(sbyte), GetParseMethod(typeof(sbyte))},
        {typeof(int), GetParseMethod(typeof(int))},
        {typeof(long), GetParseMethod(typeof(long))},
        {typeof(short), GetParseMethod(typeof(short))},
        {typeof(uint), GetParseMethod(typeof(uint))},
        {typeof(ulong), GetParseMethod(typeof(ulong))},
        {typeof(ushort), GetParseMethod(typeof(ushort))},
        {typeof(char), GetParseMethod(typeof(char))},
        {typeof(bool), GetParseMethod(typeof(bool))},
        {typeof(decimal), GetParseMethod(typeof(decimal))},
        {typeof(nint), GetParseMethod(typeof(nint))},
        {typeof(float), GetParseMethod(typeof(float))},
        {typeof(double), GetParseMethod(typeof(double))},
        {typeof(Guid), GetParseMethod(typeof(Guid))},
        {typeof(byte?), GetParseMethod(typeof(byte))},
        {typeof(sbyte?), GetParseMethod(typeof(sbyte))},
        {typeof(int?), GetParseMethod(typeof(int))},
        {typeof(long?), GetParseMethod(typeof(long))},
        {typeof(short?), GetParseMethod(typeof(short))},
        {typeof(uint?), GetParseMethod(typeof(uint))},
        {typeof(ulong?), GetParseMethod(typeof(ulong))},
        {typeof(ushort?), GetParseMethod(typeof(ushort))},
        {typeof(char?), GetParseMethod(typeof(char))},
        {typeof(bool?), GetParseMethod(typeof(bool))},
        {typeof(decimal?), GetParseMethod(typeof(decimal))},
        {typeof(nint?), GetParseMethod(typeof(nint))},
        {typeof(float?), GetParseMethod(typeof(float))},
        {typeof(double?), GetParseMethod(typeof(double))},
        {typeof(Guid?), GetParseMethod(typeof(Guid))},
    }.ToImmutableDictionary();

    static MethodInfo GetParseMethod(Type type) {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(x => x.Name == "Parse")
            .Where(x => {
                var parameters = x.GetParameters();
                return parameters.Length == 1 && parameters[0].ParameterType == typeof(string);
            }).Single();
    }

    public static bool TryParsePrimitiveValue<TResult>(this Table table, out TResult? value) {
        var type = typeof(TResult);
        if (type == typeof(string)) {
            value = FlipHorizontalTableToVertical(table, type).Rows[0][1].As<TResult>();
            return true;
        }

        if (!ParsedTypes.TryGetValue(type, out var parseMethod)) {
            value = default;
            return false;
        }

        var text = FlipHorizontalTableToVertical(table, type).Rows[0][1]?.Trim();
        if (string.IsNullOrWhiteSpace(text)) {
            value = default;
            return false;
        }

        value = parseMethod.Invoke(null, new[] {text}).As<TResult>();
        return true;
    }

    public static bool ContainsSingleJsonValue(this Table table) {
        return table.Header.Count == 1 && table.Header.First().Equals("json", StringComparison.InvariantCultureIgnoreCase);
    }

    static bool TryGetFromJson<TResult>(this Table table, JsonSerializerOptions? serializerOptions, out TResult? value) {
        if (table.ContainsSingleJsonValue()) {
            return TryDeserialize(table.Rows[0][0], serializerOptions, out value);
        }

        value = default;
        return false;

        static bool TryDeserialize(string text, JsonSerializerOptions? serializerOptions, out TResult? value) {
            try {
                value = JsonSerializer.Deserialize<TResult>(text, serializerOptions ?? DefaultJsonSerializerOptions.Instance);
                return true;
            }
            catch {
                value = default;
                return false;
            }
        }
    }


    static bool IsNullOrEmpty(string? value) {
        return string.IsNullOrWhiteSpace(value) || value.Trim().Equals("null", StringComparison.InvariantCultureIgnoreCase);
    }


    public static Table FixByteArrayAsHexOrBase64Strings(this Table table, Type targetType) {
        var byteArrayProps = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x.PropertyType.IsAssignableFrom(typeof(byte[])))
            .Select(x => x.Name)
            .ToHashSet();

        if (!byteArrayProps.Any())
            return table;

        if (table.ThisIsAVerticalTable(targetType)) {
            var fixedTable = new Table(table.Header.ToArray());
            foreach (var row in table.Rows) {
                var name = row[0];
                var value = row[1];
                fixedTable.AddRow(name, ValueFor(name, value, byteArrayProps));
            }

            return fixedTable;
        }
        else {
            var header = table.Header.ToArray();
            var fixedTable = new Table(header);
            foreach (var row in table.Rows) {
                var cells = new string[header.Length];
                for (var i = 0; i < header.Length; i++) {
                    var name = header[i];
                    var value = row[i];
                    cells[i] = ValueFor(name, value, byteArrayProps);
                }

                fixedTable.AddRow(cells.ToArray());
            }

            return fixedTable;
        }

        static string ValueFor(string propName, string value, HashSet<string> byteArrayProps) {
            return byteArrayProps.Contains(propName)
                ? string.Join(", ", ByteArrayFrom(value))
                : value;
        }

        static byte[] ByteArrayFrom(string value) {
            if (value.IsHexString()) {
                return Convert.FromHexString(value);
            }

            if (value.IsBase64String()) {
                return Convert.FromBase64String(value);
            }

            var numbers = value.Split(",").Select(x => byte.TryParse(x.Trim(), out var num) ? (byte?)num : null).ToList();
            return numbers.All(x => x != null)
                ? numbers.Select(x => x!.Value).ToArray()
                : Encoding.UTF8.GetBytes(value);
        }
    }

    public static object To(this Table table, Type type, JsonSerializerOptions? serializerOptions, Func<PropertyInfo, Type>? getInstancePropertyType = null, bool ignoreReadOnlyProps = false) {
        serializerOptions ??= DefaultJsonSerializerOptions.Instance;
        getInstancePropertyType ??= x => x.PropertyType;

        var method = ToGenericMethodDefinition.MakeGenericMethod(type);
        return method.Invoke(null, new object?[] {table, serializerOptions, getInstancePropertyType, ignoreReadOnlyProps})!;
    }

    public static TResult To<TResult>(this Table table, JsonSerializerOptions? serializerOptions, Func<PropertyInfo, Type>? getInstancePropertyType = null, bool ignoreReadOnlyProps = false) {
        serializerOptions ??= DefaultJsonSerializerOptions.Instance;
        getInstancePropertyType ??= x => x.PropertyType;

        if (table.TryGetFromJson(serializerOptions, out TResult? value)) {
            return value.As<TResult>();
        }

        var type = typeof(TResult);
        table = FlipHorizontalTableToVertical(table, type);

        if (table.TryParsePrimitiveValue(out value)) {
            return value.As<TResult>();
        }


        if (ignoreReadOnlyProps) {
            var readOnlyProps = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(x => !x.CanWrite)
                .Select(x => x.Name)
                .ToHashSet();

            var newTable = new Table("Field", "Value");
            foreach (var row in table.Rows) {
                if (readOnlyProps.Contains(row[0]))
                    continue;

                newTable.AddRow(row);
            }

            table = newTable;
        }

        table = table.FixByteArrayAsHexOrBase64Strings(typeof(TResult));
        var result = table.CreateInstance<TResult>();

        var nestedGroups = table.Rows
            .Select(row => {
                var split = row[0].Split('.');
                return new {
                    Row = row,
                    OuterName = split.Length > 1 ? split[0] : null,
                    NestedName = string.Join('.', split[1..])
                };
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.OuterName))
            .GroupBy(x => x.OuterName)
            .ToList();

        foreach (var group in nestedGroups) {
            var nestedObjectTable = new Table("Field", "Value");
            foreach (var item in group) {
                nestedObjectTable.AddRow(item.NestedName, item.Row[1]);
            }

            var resultObjectProperty = type.GetProperty(group.Key!);
            if (resultObjectProperty == null) {
                continue;
            }

            var createMethod = ToGenericMethodDefinition.MakeGenericMethod(getInstancePropertyType(resultObjectProperty));
            var valueForResultObjectProperty = createMethod.Invoke(null, new object?[] {nestedObjectTable, serializerOptions, getInstancePropertyType, ignoreReadOnlyProps});
            resultObjectProperty.SetValue(result, valueForResultObjectProperty);
        }

        foreach (var row in table.Rows.Where(row => !string.IsNullOrWhiteSpace(row[1]))) {
            var property = result!.GetType().GetProperty(row[0]);
            if (property == null)
                continue;

            var resultedValue = property.GetValue(result);
            if (resultedValue != null) {
                if (resultedValue is not IEnumerable or string) {
                    continue;
                }

                var collection = ((IEnumerable) resultedValue).Cast<object>().ToList();
                if (collection.Any() && collection.All(x => x != null)) {
                    continue;
                }
            }

            var propType = getInstancePropertyType(property);
            var castOperator = propType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(x => (x.Name == "op_Implicit" || x.Name == "op_Explicit"))
                .FirstOrDefault(x => x.GetParameters()[0].ParameterType == typeof(string));

            if (castOperator == null && propType != property.PropertyType) {
                castOperator = property.PropertyType
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(x => (x.Name == "op_Implicit" || x.Name == "op_Explicit"))
                    .FirstOrDefault(x => x.GetParameters()[0].ParameterType == typeof(string));
            }


            var stringValue = row[1];

            if (castOperator != null) {
                resultedValue = castOperator.Invoke(null, new object?[] {stringValue});
                property.SetValue(result, resultedValue);
                continue;
            }

            try {
                var resultValue = JsonSerializer.Deserialize(stringValue, getInstancePropertyType(property), serializerOptions ?? DefaultJsonSerializerOptions.Instance);
                property.SetValue(result, resultValue);
            }
            catch {
                // ignored
            }
        }

        return result;

    }

    public static Table FlipHorizontalTableToVertical(this Table table, Type type) {
        if (table.ThisIsAVerticalTable(type)) {
            return table;
        }

        var instanceTable = new Table("Field", "Value");
        foreach (var header in table.Header)
            instanceTable.AddRow(header, table.Rows[0][header]);
        return instanceTable;
    }


    public static bool ThisIsAVerticalTable(this Table table, Type type) {
        if (TheHeaderIsTheOldFieldValuePair(table))
            return true;

        return (table.Rows.Count() != 1) || (table.Header.Count == 2 && TheFirstRowValueIsTheNameOfAProperty(table, type));

        static bool TheHeaderIsTheOldFieldValuePair(Table table) {
            return table.Header.Count == 2
                   && (table.Header.First().Equals("field", StringComparison.InvariantCultureIgnoreCase) || table.Header.First().Equals("key", StringComparison.InvariantCultureIgnoreCase))
                   && table.Header.Last().Equals("value", StringComparison.InvariantCultureIgnoreCase);
        }

        static bool TheFirstRowValueIsTheNameOfAProperty(Table table, Type type) {
            var firstRowValue = table.Rows[0][table.Header.First()];
            return type.GetProperties()
                .Any(property => IsMemberMatchingToColumnName(property, firstRowValue));
        }

        static bool IsMemberMatchingToColumnName(MemberInfo member, string columnName) {
            return MatchesThisColumnName(member.Name, columnName);
        }

        static bool MatchesThisColumnName(string propertyName, string columnName) {
            var normalizedColumnName = NormalizePropertyNameToMatchAgainstAColumnName(RemoveAllCharactersThatAreNotValidInAPropertyName(columnName));
            var normalizedPropertyName = NormalizePropertyNameToMatchAgainstAColumnName(propertyName);

            return normalizedPropertyName.Equals(normalizedColumnName, StringComparison.OrdinalIgnoreCase);
        }

        static string RemoveAllCharactersThatAreNotValidInAPropertyName(string name) {
            //Unicode groups allowed: Lu, Ll, Lt, Lm, Lo, Nl or Nd see https://msdn.microsoft.com/en-us/library/aa664670%28v=vs.71%29.aspx
            return InvalidPropertyNameRegex.Replace(name, string.Empty);
        }

        static string NormalizePropertyNameToMatchAgainstAColumnName(string name) {
            // we remove underscores, because they should be equivalent to spaces that were removed too from the column names
            // we also ignore accents
            return name.Replace("_", string.Empty).ToIdentifier();
        }

    }

    static readonly Regex InvalidPropertyNameRegex = new Regex(@"[^\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}\p{Nd}_]", RegexOptions.Compiled);
}
