using System.Collections.Concurrent;
using System.Reflection;
using Dcc.Reflection.TypeFormatting;

namespace Dcc.Reflection.TypeResolver;

#if NET7_0_OR_GREATER
public class TypeResolver : ITypeResolver {
#else
public class TypeResolver {
#endif

    static ITypeResolverOptions _globalOptions = new TypeResolverOptions();
    static readonly object Lock = new();
    static volatile bool _isAlreadyMapped;

    public static void Configure(Action<ITypeResolverOptions> configure) {
        lock (Lock) {
            if (_isAlreadyMapped) {
                throw new Exception($"{nameof(TypeResolver)} is already initialized. You should configure global options before resolving any types");
            }

            var options = new TypeResolverOptions();
            configure.Invoke(options);
            _globalOptions = options.Clone();
        }
    }

    public static void Configure(Func<ITypeResolverOptions> getOptions, Action<ITypeResolverOptions> configure) {
        lock (Lock) {
            if (_isAlreadyMapped) {
                throw new Exception($"{nameof(TypeResolver)} is already initialized. You should configure global options before resolving any types");
            }

            var options = getOptions();
            configure.Invoke(options);
            _globalOptions = options.Clone();
        }
    }

    public static void MapTypes(IEnumerable<Type> types, TypeNameFormatter? formatter = null) {
        foreach (var mapping in CreateTypesMapping(types, formatter ?? _globalOptions.TypeNameFormatter)) {
            UserDefinedTypes.TryAdd(mapping.Key, mapping.Value);
        }
    }

    /// <summary>
    /// Резолвит тип по имени
    /// <para>Сперва производится попытка резолва по полному имени типа, затем - по короткому</para>
    /// </summary>
    /// <param name="formattedTypeName">
    /// Отформатированное имя типа, например короткие имена - <![CDATA[SomeGenericType<GenericParam>]]> или <![CDATA[SomeNonGenericType]]>, или полные имена - <![CDATA[Full.Name.Of.SomeGenericType<GenericParam>]]> или <![CDATA[Full.Name.Of.SomeNonGenericType]]>
    /// </param>
    /// <param name="additionalMapping">Дополнительные, кустомные, маппинги названий типов к этим типам - используемые только в текущем вызове метода. Не влияют на глобальный маппинг типов <see cref="ITypeResolver"/></param>
    /// <returns>Возвращает запрошенный тип, если он был найден</returns>
    public static Type? Resolve(ReadOnlySpan<char> formattedTypeName, IDictionary<string, Type>? additionalMapping = null) {
        var hierarchy = _globalOptions.TypeNameFormatter.GetHierarchy(ref formattedTypeName);
        return GetType(hierarchy, additionalMapping, isFullName: true) ?? GetType(hierarchy, additionalMapping, isFullName: false);
    }

    /// <summary>
    /// Резолвит тип по имени
    /// <para>Сперва производится попытка резолва по полному имени типа, затем - по короткому</para>
    /// </summary>
    /// <param name="formattedTypeName">
    /// Отформатированное имя типа, например короткие имена - <![CDATA[SomeGenericType<GenericParam>]]> или <![CDATA[SomeNonGenericType]]>, или полные имена - <![CDATA[Full.Name.Of.SomeGenericType<GenericParam>]]> или <![CDATA[Full.Name.Of.SomeNonGenericType]]>
    /// </param>
    /// <param name="additionalTypes">Дополнительные, кустомные, типы, для которых будет сформирован маппинг названий, согласно глобальной конфигурации - и используемые только в текущем вызове метода. Не влияют на глобальный маппинг типов <see cref="ITypeResolver"/></param>
    /// <returns>Возвращает запрошенный тип, если он был найден</returns>
    public static Type? Resolve(ReadOnlySpan<char> formattedTypeName, IEnumerable<Type> additionalTypes) {
        var hierarchy = _globalOptions.TypeNameFormatter.GetHierarchy(ref formattedTypeName);
        var additionalMapping = CreateTypesMapping(additionalTypes, _globalOptions.TypeNameFormatter);
        return GetType(hierarchy, additionalMapping, isFullName: true) ?? GetType(hierarchy, additionalMapping, isFullName: false);
    }


    static readonly TypeNameFormatter _fullNameFormatter = new TypeFullNameFormatter();

    /// <summary>
    /// Резолвит тип по полному, отформатированному, имени типа
    /// </summary>
    /// <param name="formattedFullName">
    /// Отформатированное имя типа, например <![CDATA[Full.Name.Of.SomeGenericType<GenericParam>]]> или <![CDATA[Full.Name.Of.SomeNonGenericType]]>
    /// </param>
    /// <param name="additionalMapping">Дополнительные, кустомные, маппинги названий типов к этим типам - используемые только в текущем вызове метода. Не влияют на глобальный маппинг типов <see cref="ITypeResolver"/></param>
    /// <returns>Возвращает запрошенный тип, если он был найден по полному имени</returns>
    public static Type? ResolveByFullName(ReadOnlySpan<char> formattedFullName, IDictionary<string, Type>? additionalMapping = null) {
        var hierarchy = _fullNameFormatter.GetHierarchy(ref formattedFullName);
        return GetType(hierarchy, additionalMapping, isFullName: true);
    }

    static Type? GetType(TypeNameFormatter.TypeNameHierarchy hierarchy, IDictionary<string, Type>? additionalMapping = null, bool isFullName = false) {
        var formattedName = _globalOptions.TypeNameFormatter.GetFormattedName(hierarchy);

        var type = FindType(formattedName, additionalMapping, isFullName);
        if (type == null || !hierarchy.Generics.Any())
            return type;

        var parameters = new Type[hierarchy.Generics.Count];
        for (var i = 0; i < hierarchy.Generics.Count; i++) {
            var parameterType = GetType(hierarchy.Generics[i]);
            if (parameterType == null) {
                return null;
            }
            parameters[i] = parameterType;
        }

        return type!.MakeGenericType(parameters);
    }

    static Type? FindType(string formattedName, IDictionary<string, Type>? additionalMapping, bool isFullName = false) {
        if (additionalMapping?.TryGetValue(formattedName, out var type) == true) {
            return type;
        }

        if (UserDefinedTypes.TryGetValue(formattedName, out type)) {
            return type;
        }

        var globalMapping = isFullName
            ? FormattedFullNamedTypes.Value
            : FormattedTypes.Value;

        return globalMapping.TryGetValue(formattedName, out type) ? type : null;
    }

    static readonly ConcurrentDictionary<string, Type> UserDefinedTypes = new();

    static readonly Lazy<Dictionary<string, Type>> FormattedTypes = new(() => {
        var mapping = CreateTypesMapping(GetTypesFromAssemblies(), _globalOptions.TypeNameFormatter);
        _isAlreadyMapped = true;
        return mapping;
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    static readonly Lazy<Dictionary<string, Type>> FormattedFullNamedTypes = new(() => {
        var mapping = CreateTypesMapping(GetTypesFromAssemblies(), new TypeFullNameFormatter());
        _isAlreadyMapped = true;
        return mapping;
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    static Dictionary<string, Type> CreateTypesMapping(IEnumerable<Type> types, TypeNameFormatter formatter) {
        var mapping = new Dictionary<string, Type>();

        foreach (var type in types) {
            var name = type.GetNestedName(formatter);
            if (mapping.TryAdd(name, type)) {
                continue;
            }

            mapping.Remove(name, out var conflictedType);
            var unConflictedName1 = formatter.GetUniqueTypeName(conflictedType!);
            var unConflictedName2 = formatter.GetUniqueTypeName(type);
            if (unConflictedName1 == unConflictedName2) {
                if (_globalOptions.ThrowOnUnresolvedNameConflicts) {
                    throw new InvalidOperationException($"Type {conflictedType!.FullName} and {type.FullName} formatted with same name, event after conflict name resolving!");
                }

                continue;
            }

            if (!mapping.TryAdd(unConflictedName1, conflictedType!) && _globalOptions.ThrowOnUnresolvedNameConflicts) {
                throw new InvalidOperationException($"Type {conflictedType!.FullName} and {type.FullName} formatted with same name, event after conflict name resolving!");
            }

            if (!mapping.TryAdd(unConflictedName2, type) && _globalOptions.ThrowOnUnresolvedNameConflicts) {
                throw new InvalidOperationException($"Type {conflictedType!.FullName} and {type.FullName} formatted with same name, event after conflict name resolving!");
            }
        }

        return mapping;
    }

    static IEnumerable<Type> GetTypesFromAssemblies() {
        var assemblies = GetAssemblyNames();

        foreach (var file in assemblies) {
            Assembly assembly;
            try {
                assembly = Assembly.Load(file);
            }
            catch (Exception e) {
                Console.WriteLine(e.ToString());
                //todo: add logging
                continue;
            }

            IEnumerator<Type> exportedTypes;
            try {
                exportedTypes = assembly.ExportedTypes.GetEnumerator();
            }
            catch (Exception e) {
                Console.WriteLine(e);
                continue;
            }

            do {
                Type? type = null;
                try {
                    if (exportedTypes.MoveNext()) {
                        type = exportedTypes.Current;
                        if (type!.IsGenericParameter || _globalOptions.TypeExclude(type))
                            continue;
                    }
                }
                catch (Exception e) {
                    exportedTypes.Dispose();
                    Console.WriteLine(e);
                    break;
                }

                if (type == null)
                    break;

                yield return type;

            } while (true);

            exportedTypes.Dispose();
        }
    }

    static IEnumerable<string> GetAssemblyNames() {
        if (_globalOptions.AssemblyNamesResolver != null) {
            return _globalOptions.AssemblyNamesResolver.Invoke().Union(_globalOptions.AdditionalAssemblies).Distinct();
        }

        var location = typeof(TypeResolver).Assembly.Location;
        var path = Path.GetDirectoryName(location);
        var names = Directory.GetFiles(path!, "*.dll")
            .Select(Path.GetFileName)
            .Select(x => x!.Substring(0, x.Length - 4))
            .Where(x => _globalOptions.AssemblyInclude(x) || !_globalOptions.AssemblyExclude(x))
            .Union(_globalOptions.AdditionalAssemblies)
            .Distinct();

        return names;
    }
}

