using System.Text;
using LinkDotNet.StringBuilder;

namespace Dcc.Reflection.TypeFormatting;

public static class TypeFormattingExtensions {

    public static string FormatTypeName(this Type type, TypeNameFormatter formatter) {
        return type.FormatTypeName(formatter.GetTypeName, formatter.FormatGenericDefinitionArgs, formatter.GetNestedNameSplitter());
    }

    public static string FormatTypeName(this Type type, Func<Type, string> nameSelector, Action<StringBuilder, IEnumerable<string?>>? genericFormatter = null, char nestedNameSplitter = '.') {
        var name = nameSelector(type);
        if (!type.IsGenericType) {
            return name;
        }

        var index = name.IndexOf('`');
        if (index < 0) {
            return name;
        }

        var builder = new StringBuilder(name.Substring(0, index));
        genericFormatter ??= PrettyFormatGeneric;
        var args = type.GetGenericArguments();
        if (args.Length > 0) {
            genericFormatter.Invoke(builder, args.Select(x => x.IsGenericParameter ? null : GetNestedName(x, nameSelector, genericFormatter, nestedNameSplitter)));
        }


        return builder.ToString();
    }

    public static string FormatTypeName(this Type type, Func<Type, string> nameSelector, Span<Type> genericArgs, Action<StringBuilder, IEnumerable<string?>>? genericFormatter = null, char nestedNameSplitter = '.') {
        var name = nameSelector(type);
        if (!type.IsGenericType) {
            return name;
        }

        var index = name.IndexOf('`');
        if (index < 0) {
            return name;
        }

        var builder = new StringBuilder(name.Substring(0, index));
        genericFormatter ??= PrettyFormatGeneric;
        if (genericArgs.Length > 0) {
            genericFormatter.Invoke(builder, GetNestedNames(genericArgs, genericArg => GetNestedName(genericArg, nameSelector, genericFormatter, nestedNameSplitter)));
        }

        return builder.ToString();

    }

    static void AppendTypeName(ref ValueStringBuilder builder, Type type, Span<Type?> genericArgs, TypeNameFormatter formatter) {
        var name = formatter.GetTypeName(type);
        if (!type.IsGenericType) {
            builder.Append(name);
            return;
        }

        var index = name.IndexOf('`');
        if (index < 0) {
            builder.Append(name);
            return;
        }

        builder.Append(name.AsSpan()[..index]);
        if (genericArgs.Length > 0) {
            formatter.AppendGenericDefinitionArgs(ref builder, genericArgs);
        }
    }

    static int InsertTypeName(ref ValueStringBuilder builder, Type type, Span<Type?> genericArgs, TypeNameFormatter formatter, int startIndex = 0) {
        var name = formatter.GetTypeName(type);
        if (!type.IsGenericType) {
            builder.Insert(startIndex, name);
            return startIndex + name.Length;
        }

        var index = name.IndexOf('`');
        if (index < 0) {
            builder.Insert(startIndex, name);
            return startIndex + name.Length;
        }


        var n = name.AsSpan()[..index];
        builder.Insert(startIndex, n);
        var position = startIndex + n.Length;

        return genericArgs.Length > 0
            ? formatter.InsertGenericDefinitionArgs(ref builder, position, genericArgs)
            : position;
    }

    static List<string?> GetNestedNames(Span<Type> types, Func<Type, string> nameSelector) {
        var list = new List<string?>();

        var enumerator = types.GetEnumerator();
        while (enumerator.MoveNext()) {
            if (enumerator.Current.IsGenericParameter) {
                list.Add(null);
                continue;
            }

            list.Add(nameSelector.Invoke(enumerator.Current));
        }

        return list;
    }

    public static string GetNestedName(this Type type, TypeNameFormatter formatter, bool removeRootNamespaceDuplicates = false) {
        var genericArgsMap = type.GetNestingGenericArgumentsMap();
        var builder = new ValueStringBuilder();
        try {
            AppendTypeName(ref builder, type, genericArgsMap[type], formatter);

            var nestedNameSplitter = formatter.GetNestedNameSplitter();
            while (type is {DeclaringType: { }, IsGenericTypeParameter: false}) {
                type = type.DeclaringType;

                builder.Insert(0, nestedNameSplitter);
                InsertTypeName(ref builder, type, genericArgsMap[type], formatter);
            }

            if (removeRootNamespaceDuplicates && type.Namespace != null) {
                var space = builder.AsSpan()[..(type.Namespace!.Length + 1)];
                try {
                    builder.Replace(space, "", space.Length, builder.Length - space.Length);
                }
                catch (Exception e) {
                    Console.WriteLine(e);
                }
            }

            return builder.ToString();
        }
        finally {
            builder.Dispose();
        }
    }

    public static int InsertNestedName(this Type type, ref ValueStringBuilder builder, int startIndex, TypeNameFormatter formatter) {
        var genericArgsMap = type.GetNestingGenericArgumentsMap();
        var position = InsertTypeName(ref builder, type, genericArgsMap[type], formatter, startIndex);

        var nestedNameSplitter = formatter.GetNestedNameSplitter();

        while (type is {DeclaringType: { }, IsGenericTypeParameter: false}) {
            type = type.DeclaringType;

            builder.Insert(startIndex, nestedNameSplitter);
            position++;
            position += InsertTypeName(ref builder, type, genericArgsMap[type], formatter, startIndex) - startIndex;
        }

        return position;
    }

    public static string GetNestedName(this Type type, Func<Type, string> nameSelector, Action<StringBuilder, IEnumerable<string?>>? genericFormatter = null, char nestedNameSplitter = '.') {
        var genericArgsMap = type.GetNestingGenericArgumentsMap();

        var builder = new StringBuilder(FormatTypeName(type, nameSelector, genericArgsMap[type], genericFormatter, nestedNameSplitter));

        while (type is {DeclaringType: { }, IsGenericTypeParameter: false}) {
            type = type.DeclaringType;
            builder.Insert(0, nestedNameSplitter).Insert(0, FormatTypeName(type, nameSelector, genericArgsMap[type], genericFormatter, nestedNameSplitter));
        }

        return builder.ToString();
    }

    static int GetGenericStartPosition(Type? type, int start = 0) {
        while ((type = type?.DeclaringType) != null) {
            start += type.GetGenericArguments().Length;
        }

        return start;
    }

    public static int GetActualGenericsCount(this Type type) {
        var total = type.GetGenericArguments().Length;
        var declared = type.DeclaringType?.GetGenericArguments().Length ?? 0;
        return total - declared;
    }

    public static Dictionary<Type, ArraySegment<Type?>> GetNestingGenericArgumentsMap(this Type type) {
        var result = new Dictionary<Type, ArraySegment<Type?>>();

        foreach (var item in GetNestingGenericArguments(type)) {
            result.Add(item.Type, item.Generics);
        }

        return result;
    }

    public static IEnumerable<(Type Type, ArraySegment<Type?> Generics)> GetNestingGenericArguments(this Type type) {
        var genericArgs = type.GenericTypeArguments;
        var usedArgs = 0;

        while (type != null) {
            if (genericArgs.Length == 0) {
                yield return (type, type.IsGenericTypeDefinition ? new(new Type[type.GetGenericArguments().Length]) : ArraySegment<Type?>.Empty);
                type = type.DeclaringType;
                continue;
            }

            var count = GetActualGenericsCount(type);
            if (count <= 0) {
                yield return (type, ArraySegment<Type?>.Empty);
                type = type.DeclaringType;
                continue;
            }

            var start = genericArgs.Length - usedArgs - count;
            (Type Type, ArraySegment<Type?> Generics) item;
            try {
                item = (type, new(genericArgs, start, count));
            }
            catch (Exception e) {
                throw new InvalidOperationException($"Can't {nameof(GetNestingGenericArguments)} of Type {type.FullName}", e);
            }

            yield return item;

            usedArgs += count;
            type = type.DeclaringType;
        }
    }

    static readonly Func<Type, string> NameSelector = type => type.Name;
    static readonly Func<Type, string> FullNameSelector = type => type.FullName!;

    static readonly TypeNameFormatter ShortNameFormatter = new TypeShortNameFormatter();
    static readonly TypeNameFormatter FullNameFormatter = new TypeFullNameFormatter();
    static readonly Action<StringBuilder, IEnumerable<string?>> PrettyFormatGeneric = ShortNameFormatter.FormatGenericDefinitionArgs;


    public static string GetNestedName(this Type type, Action<StringBuilder, IEnumerable<string?>>? genericFormatter, char nestedNameSplitter = '.') {
        return GetNestedName(type, ShortNameFormatter.GetTypeName, genericFormatter, nestedNameSplitter);
    }

    public static string GetNestedName(this Type type) {
        return GetNestedName(type, ShortNameFormatter);
    }

    public static string GetNestedFullName(this Type type, Action<StringBuilder, IEnumerable<string?>>? genericFormatter, char nestedNameSplitter = '.') {
        return GetNestedName(type, FullNameFormatter.GetTypeName, genericFormatter, nestedNameSplitter);
    }

    public static string GetNestedFullName(this Type type) {
        return GetNestedName(type, FullNameFormatter);
    }

    public static string GetNestedFullNameWithoutNamespaceDuplicates(this Type type) {
        return GetNestedName(type, FullNameFormatter, removeRootNamespaceDuplicates: true);
    }

}
