using Dcc.Reflection.TypeFormatting;

namespace Dcc.Reflection.TypeResolver;

public class TypeResolverOptions {
    public TypeNameFormatter TypeNameFormatter { get; set; } = new TypeShortNameFormatter();
    public Func<string, bool> AssemblyExclude { get; set; } = name => name.Contains("EntityFrameworkCore") && name.Contains("Design");
    public Func<string, bool> AssemblyInclude { get; set; } = _ => false;
    public Func<Type, bool> TypeExclude { get; set; } = _ => false;
    public bool ThrowOnUnresolvedNameConflicts { get; set; }
    public Func<IEnumerable<string>>? AssemblyNamesResolver { get; set; }

    public IEnumerable<string> AdditionalAssemblies { get; set; } = ArraySegment<string>.Empty;

    internal TypeResolverOptions Clone() => (TypeResolverOptions) MemberwiseClone();
}
