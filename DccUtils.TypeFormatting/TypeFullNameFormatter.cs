namespace Dcc.Reflection.TypeFormatting;

public sealed class TypeFullNameFormatter : TypeNameFormatter {
    public override string GetTypeName(Type type) => type.IsNested ? type.Name : type.FullName!;
}
