namespace Dcc.Reflection.Extensions.TypeFormatter;

public sealed class TypeShortNameFormatter : TypeNameFormatter {
    public override string GetTypeName(Type type) => type.Name;
}