namespace Dcc.Reflection.Extensions.TypeFormatter;

public sealed class TypeFullNameFormatter : TypeNameFormatter {
    public override string GetTypeName(Type type) => type.FullName!;
}