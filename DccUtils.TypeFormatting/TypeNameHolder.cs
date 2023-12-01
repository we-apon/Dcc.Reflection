namespace Dcc.Reflection.TypeFormatting;

public static class TypeNameHolder<T> {
    public static readonly string NestedName = typeof(T).GetNestedName(dontCache: true);
    public static readonly string NestedFullName = typeof(T).GetNestedFullName(dontCache: true);
    public static readonly string NestedFullNameWithoutNamespaceDuplicates = typeof(T).GetNestedFullNameWithoutNamespaceDuplicates(dontCache: true);
}

