namespace Dcc.Reflection.TypeFormatting;

public sealed class TypeShortNameFormatter : TypeNameFormatter {
    public override string GetTypeName(Type type) => type.Name;
}
