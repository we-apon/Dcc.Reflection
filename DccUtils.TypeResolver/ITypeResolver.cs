#if NET7_0_OR_GREATER

using Dcc.Reflection.TypeFormatting;

namespace Dcc.Reflection.TypeResolver;

public interface ITypeResolver {
    static abstract void Configure(Action<ITypeResolverOptions> configure);
    static abstract void Configure(Func<ITypeResolverOptions> getOptions, Action<ITypeResolverOptions> configure);
    static abstract void MapTypes(IEnumerable<Type> types, TypeNameFormatter? formatter = null);

    /// <summary>
    /// Резолвит тип по имени
    /// <para>Сперва производится попытка резолва по короткому имени типа, затем - по полному без дубликатов неймспейсов, затем - по полному</para>
    /// </summary>
    /// <param name="formattedTypeName">
    /// Отформатированное имя типа, например короткие имена - <![CDATA[SomeGenericType<GenericParam>]]> или <![CDATA[SomeNonGenericType]]>, или полные имена - <![CDATA[Full.Name.Of.SomeGenericType<GenericParam>]]> или <![CDATA[Full.Name.Of.SomeNonGenericType]]>
    /// </param>
    /// <param name="additionalMapping">Дополнительные, кустомные, маппинги названий типов к этим типам - используемые только в текущем вызове метода. Не влияют на глобальный маппинг типов <see cref="ITypeResolver"/></param>
    /// <returns>Возвращает запрошенный тип, если он был найден</returns>
    static abstract Type? Resolve(ReadOnlySpan<char> formattedTypeName, IDictionary<string, Type>? additionalMapping = null);


    /// <summary>
    /// Резолвит тип по имени
    /// <para>Сперва производится попытка резолва по короткому имени типа, затем - по полному без дубликатов неймспейсов, затем - по полному</para>
    /// </summary>
    /// <param name="formattedTypeName">
    /// Отформатированное имя типа, например короткие имена - <![CDATA[SomeGenericType<GenericParam>]]> или <![CDATA[SomeNonGenericType]]>, или полные имена - <![CDATA[Full.Name.Of.SomeGenericType<GenericParam>]]> или <![CDATA[Full.Name.Of.SomeNonGenericType]]>
    /// </param>
    /// <param name="additionalTypes">Дополнительные, кустомные, типы, для которых будет сформирован маппинг названий, согласно глобальной конфигурации - и используемые только в текущем вызове метода. Не влияют на глобальный маппинг типов <see cref="ITypeResolver"/></param>
    /// <returns>Возвращает запрошенный тип, если он был найден</returns>
    static abstract Type? Resolve(ReadOnlySpan<char> formattedTypeName, IEnumerable<Type> additionalTypes);

    /// <summary>
    /// Резолвит тип по полному, отформатированному, имени типа
    /// <para>Сперва производится попытка резолва по полному имени без дубликатов неймспейсов, затем - по полному</para>
    /// </summary>
    /// <param name="formattedFullName">
    /// Отформатированное имя типа, например <![CDATA[Full.Name.Of.SomeGenericType<GenericParam>]]> или <![CDATA[Full.Name.Of.SomeNonGenericType]]>
    /// </param>
    /// <param name="additionalMapping">Дополнительные, кустомные, маппинги названий типов к этим типам - используемые только в текущем вызове метода. Не влияют на глобальный маппинг типов <see cref="ITypeResolver"/></param>
    /// <returns>Возвращает запрошенный тип, если он был найден по полному имени</returns>
    static abstract Type? ResolveByFullName(ReadOnlySpan<char> formattedFullName, IDictionary<string, Type>? additionalMapping = null);
}

#endif
