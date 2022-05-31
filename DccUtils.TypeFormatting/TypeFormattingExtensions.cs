using System.Text;

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

    public static string GetNestedName(this Type type, TypeNameFormatter formatter) {
        return GetNestedName(type, formatter.GetTypeName, formatter.FormatGenericDefinitionArgs, formatter.GetNestedNameSplitter());
    }

    public static string GetNestedName(this Type type, Func<Type, string> nameSelector, Action<StringBuilder, IEnumerable<string?>>? genericFormatter = null, char nestedNameSplitter = '.') {
        var builder = new StringBuilder(FormatTypeName(type, nameSelector, genericFormatter));
        while (type.DeclaringType != null && !type.IsGenericTypeParameter) {
            type = type.DeclaringType;
            builder.Insert(0, nestedNameSplitter).Insert(0, FormatTypeName(type, nameSelector, genericFormatter));
        }

        return builder.ToString();
    }

    static readonly Func<Type, string> NameSelector = type => type.Name;
    static readonly Func<Type, string> FullNameSelector = type => type.FullName!;

    static readonly TypeNameFormatter ShortNameFormatter = new TypeShortNameFormatter();
    static readonly TypeNameFormatter FullNameFormatter = new TypeFullNameFormatter();
    static readonly Action<StringBuilder, IEnumerable<string?>> PrettyFormatGeneric = ShortNameFormatter.FormatGenericDefinitionArgs;



    public static string GetNestedName(this Type type, Action<StringBuilder, IEnumerable<string?>>? genericFormatter = null, char nestedNameSplitter = '.') {
        return GetNestedName(type, ShortNameFormatter.GetTypeName, genericFormatter, nestedNameSplitter);
    }

    public static string GetNestedFullName(this Type type, Action<StringBuilder, IEnumerable<string?>>? genericFormatter = null, char nestedNameSplitter = '.') {
        return GetNestedName(type, FullNameFormatter.GetTypeName, genericFormatter, nestedNameSplitter);
    }

}
