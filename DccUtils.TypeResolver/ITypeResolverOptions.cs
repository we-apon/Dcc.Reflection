using Dcc.Reflection.TypeFormatting;

namespace Dcc.Reflection.TypeResolver;

public interface ITypeResolverOptions {
    TypeNameFormatter TypeNameFormatter { get; set; }
    Func<string, bool> AssemblyExclude { get; set; }
    Func<string, bool> AssemblyInclude { get; set; }
    Func<Type, bool> TypeExclude { get; set; }
    bool ThrowOnUnresolvedNameConflicts { get; set; }
    Func<IEnumerable<string>>? AssemblyNamesResolver { get; set; }
    IEnumerable<string> AdditionalAssemblies { get; set; }
    ITypeResolverOptions Clone();
}
