using System.Collections.Concurrent;
using System.Reflection;
using Dcc.Reflection.TypeFormatting;

namespace Dcc.Reflection.TypeResolver;

public class TypeResolver : ITypeResolver {

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

    public static void MapType(string name, Type type) {
        UserDefinedTypes.TryAdd(name, type);
    }

    public static void MapTypes(IEnumerable<Type> types, TypeNameFormatter? formatter = null) {
        foreach (var mapping in CreateTypesMapping(types, formatter ?? _globalOptions.TypeNameFormatter)) {
            UserDefinedTypes.TryAdd(mapping.Key, mapping.Value);
        }
    }

    public static Type? Resolve(string formattedTypeName, IDictionary<string, Type>? additionalMapping = null) {
        var type = FindType(formattedTypeName, additionalMapping) ?? FindType(formattedTypeName, additionalMapping, isFullName: true);
        if (type != null)
            return type;

        var hierarchy = _globalOptions.TypeNameFormatter.GetHierarchy(formattedTypeName);
        return GetType(hierarchy, additionalMapping, isFullName: false) ?? GetType(hierarchy, additionalMapping, isFullName: true);
    }

    /// <summary>
    /// Резолвит тип по имени
    /// <para>Сперва производится попытка резолва по короткому имени типа, затем - по полному без дубликатов неймспейсов, затем - по полному</para>
    /// </summary>
    /// <param name="formattedTypeName">
    /// Отформатированное имя типа, например короткие имена - <![CDATA[SomeGenericType<GenericParam>]]> или <![CDATA[SomeNonGenericType]]>, или полные имена - <![CDATA[Full.Name.Of.SomeGenericType<GenericParam>]]> или <![CDATA[Full.Name.Of.SomeNonGenericType]]>
    /// </param>
    /// <param name="additionalMapping">Дополнительные, кустомные, маппинги названий типов к этим типам - используемые только в текущем вызове метода. Не влияют на глобальный маппинг типов <see cref="ITypeResolver"/></param>
    /// <returns>Возвращает запрошенный тип, если он был найден</returns>
    public static Type? Resolve(ReadOnlySpan<char> formattedTypeName, IDictionary<string, Type>? additionalMapping = null) {
        var text = formattedTypeName.Trim().ToString();
        return Resolve(text, additionalMapping);
    }

    public static Type? Resolve(string formattedTypeName, IEnumerable<Type> additionalTypes) {
        var additionalMapping = CreateTypesMapping(additionalTypes, _globalOptions.TypeNameFormatter);
        return Resolve(formattedTypeName, additionalMapping);
    }

    /// <summary>
    /// Резолвит тип по имени
    /// <para>Сперва производится попытка резолва по короткому имени типа, затем - по полному без дубликатов неймспейсов, затем - по полному</para>
    /// </summary>
    /// <param name="formattedTypeName">
    /// Отформатированное имя типа, например короткие имена - <![CDATA[SomeGenericType<GenericParam>]]> или <![CDATA[SomeNonGenericType]]>, или полные имена - <![CDATA[Full.Name.Of.SomeGenericType<GenericParam>]]> или <![CDATA[Full.Name.Of.SomeNonGenericType]]>
    /// </param>
    /// <param name="additionalTypes">Дополнительные, кустомные, типы, для которых будет сформирован маппинг названий, согласно глобальной конфигурации - и используемые только в текущем вызове метода. Не влияют на глобальный маппинг типов <see cref="ITypeResolver"/></param>
    /// <returns>Возвращает запрошенный тип, если он был найден</returns>
    public static Type? Resolve(ReadOnlySpan<char> formattedTypeName, IEnumerable<Type> additionalTypes) {
        var additionalMapping = CreateTypesMapping(additionalTypes, _globalOptions.TypeNameFormatter);
        return Resolve(formattedTypeName, additionalMapping);
    }

    public static Type? ResolveByFullName(string formattedFullName, IDictionary<string, Type>? additionalMapping = null) {
        var type = FindType(formattedFullName, additionalMapping, isFullName: true);
        if (type != null)
            return type;

        var hierarchy = _fullNameFormatter.GetHierarchy(formattedFullName);
        return GetType(hierarchy, additionalMapping, isFullName: true);
    }


    static readonly TypeNameFormatter _fullNameFormatter = new TypeFullNameFormatter();

    /// <summary>
    /// Резолвит тип по полному, отформатированному, имени типа
    /// <para>Сперва производится попытка резолва по полному имени без дубликатов неймспейсов, затем - по полному</para>
    /// </summary>
    /// <param name="formattedFullName">
    /// Отформатированное имя типа, например <![CDATA[Full.Name.Of.SomeGenericType<GenericParam>]]> или <![CDATA[Full.Name.Of.SomeNonGenericType]]>
    /// </param>
    /// <param name="additionalMapping">Дополнительные, кустомные, маппинги названий типов к этим типам - используемые только в текущем вызове метода. Не влияют на глобальный маппинг типов <see cref="ITypeResolver"/></param>
    /// <returns>Возвращает запрошенный тип, если он был найден по полному имени</returns>
    public static Type? ResolveByFullName(ReadOnlySpan<char> formattedFullName, IDictionary<string, Type>? additionalMapping = null) {
        var text = formattedFullName.Trim().ToString();
        return ResolveByFullName(text, additionalMapping);
    }

    static Type? GetType(TypeNameFormatter.TypeNameHierarchy hierarchy, IDictionary<string, Type>? additionalMapping = null, bool isFullName = false) {
        var formattedName = _globalOptions.TypeNameFormatter.GetFormattedName(hierarchy);

        var type = FindType(formattedName, additionalMapping, isFullName);
        if (type == null || hierarchy.Generics.Count == 0 || hierarchy.Generics.All(x => string.IsNullOrWhiteSpace(x.Name)))
            return type;

        var parameters = new Type[hierarchy.Generics.Count];
        for (var i = 0; i < hierarchy.Generics.Count; i++) {
            var parameterType = GetType(hierarchy.Generics[i], additionalMapping, isFullName) ?? GetType(hierarchy.Generics[i], additionalMapping, !isFullName);
            if (parameterType == null) {
                return null;
            }
            parameters[i] = parameterType;
        }

        var genericType = type.MakeGenericType(parameters);
        if (hierarchy.Nested == null)
            return genericType;

        var nestedName = $"{genericType.GetNestedName()}.{_globalOptions.TypeNameFormatter.GetFormattedName(hierarchy.Nested)}";

        foreach (var nestedType in genericType.GetNestedTypes()) {
            try {
                var genericNested = nestedType.MakeGenericType(parameters);
                var genericName = genericNested.GetNestedName();
                UserDefinedTypes.TryAdd(genericName, genericNested);

                if (genericName == nestedName) {
                    return genericNested;
                }
            }
            catch (Exception e) {
                Console.WriteLine(e);
            }
        }

        return null;
    }

    static Type? FindType(string formattedName, IDictionary<string, Type>? additionalMapping, bool isFullName = false) {
        if (additionalMapping?.TryGetValue(formattedName, out var type) == true) {
            return type;
        }

        if (!UserDefinedTypes.IsEmpty && UserDefinedTypes.TryGetValue(formattedName, out type)) {
            return type;
        }

        if (!isFullName) {
            return FormattedTypes.Value.TryGetValue(formattedName, out type) ? type : null;
        }

        return FormattedFullNamedTypesWithotNamespaceDuplicates.Value.TryGetValue(formattedName, out type)
            ? type
            : FormattedFullNamedTypes.Value.TryGetValue(formattedName, out type)
                ? type
                : null;
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

    static readonly Lazy<Dictionary<string, Type>> FormattedFullNamedTypesWithotNamespaceDuplicates = new(() => {
        var mapping = CreateTypesMapping(GetTypesFromAssemblies(), new TypeFullNameFormatter(), removeNamespaceDuplicates: true);
        _isAlreadyMapped = true;
        return mapping;
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    static Dictionary<string, Type> CreateTypesMapping(IEnumerable<Type> types, TypeNameFormatter formatter, bool removeNamespaceDuplicates = false) {
        var mapping = new Dictionary<string, Type>();

        foreach (var type in types) {
            string name;
            try {
                name = type.GetNestedName(formatter, removeNamespaceDuplicates);
            }
            catch (Exception e) {
                throw new InvalidOperationException($"Can't GetNestedName of Type {type.FullName}", e);
            }

            if (mapping.TryAdd(name, type)) {
                continue;
            }

            if (mapping.Remove(name, out var conflictedType)) {
                if (type == conflictedType) {
                    mapping.TryAdd(name, type);
                    continue;
                }
            }

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

        yield return typeof(string);
        yield return typeof(int);
        yield return typeof(int?);
        yield return typeof(long);
        yield return typeof(long?);
        yield return typeof(Int128);
        yield return typeof(Int128?);
        yield return typeof(UInt128);
        yield return typeof(UInt128?);
        yield return typeof(uint);
        yield return typeof(uint?);
        yield return typeof(ulong);
        yield return typeof(ulong?);
        yield return typeof(short);
        yield return typeof(short?);
        yield return typeof(ushort);
        yield return typeof(ushort?);
        yield return typeof(decimal);
        yield return typeof(decimal?);
        yield return typeof(byte);
        yield return typeof(byte?);
        yield return typeof(char);
        yield return typeof(char?);
        yield return typeof(bool);
        yield return typeof(bool?);

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

