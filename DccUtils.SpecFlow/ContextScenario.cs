using System.Reflection;
using System.Text.Json;
using Dcc.Reflection.TypeFormatting;
using Dcc.Reflection.TypeResolver;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TechTalk.SpecFlow;
using TechTalk.SpecFlow.Infrastructure;

namespace Dcc.SpecFlow;


public abstract class ContextScenario : ContextScenario<TypeResolver> { }

public abstract class ContextScenario<TTypeResolver> where TTypeResolver : ITypeResolver {
    JsonSerializerOptions? _serializerOptions;
    readonly object _lock = new();


    protected ScenarioContext Context { get; private set; }
    protected ISpecFlowOutputHelper Output { get; private set; }

    protected virtual TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    protected virtual JsonSerializerOptions? SerializerOptions {
        get => _serializerOptions;
        set {
            lock (_lock) {
                if (_serializerOptions != null)
                    throw new InvalidOperationException($"ContextScenario.{nameof(SerializerOptions)} already set");

                _serializerOptions = value;
            }
        }
    }

    [BeforeScenario]
    public void ContextScenarioSetup(ScenarioContext context, ISpecFlowOutputHelper outputHelper) {
        Context = context;
        Context.TryAdd(typeof(PropertyInstanceTypeMapper).FullName, new PropertyInstanceTypeMapper());
        Output = outputHelper;
    }

    [BeforeStep]
    public void ContextScenarioBeforeStep(ScenarioContext context) {
        if (SerializerOptions != null)
            return;

        if (context.TryGetValue(out JsonSerializerOptions options)) {
            SerializerOptions = options;
            return;
        }

        if (context.TryGetValue(out IServiceProvider serviceProvider)) {
            SerializerOptions = serviceProvider.GetService<JsonSerializerOptions>();
        }
    }


    protected virtual IEnumerable<Type> KnownTypes() {
        foreach (var type in TableExtensions.ParsedTypes.Keys) {
            yield return type;
        }

        yield return typeof(string);
    }

    protected T Set<T>(T value, string? name = null) {
        if (string.IsNullOrWhiteSpace(name)) {
            Context.Set(value);
        }
        else {
            Context.Add(name, value);
        }

        return value;
    }

    protected object Set(object value, string? name = null) {
        if (string.IsNullOrWhiteSpace(name)) {
            foreach (var typeName in GetTypeNames(value)) {
                Context.TryAdd(typeName, value);
            }
        }
        else {
            Context.Add(name, value);
        }

        return value;
    }

    static IEnumerable<string> GetTypeNames(object value) {
        var type = value.GetType();

        yield return type.GetNestedName();
        yield return type.GetNestedFullName();
        yield return type.GetNestedFullNameWithoutNamespaceDuplicates();
        yield return type.FullName!;
    }

    protected object Set(string typeName, Table table, string? name = null) {
        var type = TTypeResolver.Resolve(typeName, KnownTypes());

        type.Should().NotBeNull($"Тип {typeName} должен быть зарегистрирован в перегрузке метода {nameof(KnownTypes)}, или глобально в используемом {nameof(TypeResolver)}, чтобы иметь возможность использовать {nameof(Set)}({nameof(typeName)}, {nameof(table)})");

        var typeMapper = Get<PropertyInstanceTypeMapper>();
        var value = table.To(type!, SerializerOptions, typeMapper.GetInstanceTypeForProperty);
        Set(value, name);
        return value;
    }

    protected bool Has<T>() => Context.ContainsKey(typeof(T).FullName!);
    protected bool Has(string name) => Context.ContainsKey(name);
    protected bool TryGet<T>(out T? value) => Context.TryGetValue(out value);
    protected bool TryGet<T>(string name, out T? value) => Context.TryGetValue(name, out value);

    protected T Get<T>(string? name = null) {
        var value = string.IsNullOrWhiteSpace(name)
            ? Context.Get<T>()
            : Context.Get<T>(name);

        if (value != null || name != null) {
            return value;
        }

        var provider = Context.Get<IServiceProvider>();
        if (provider != null) {
            value = provider.GetService<T>();
        }

        return value!;
    }


    protected object GetByTypeName(string typeName) {
        var type = TTypeResolver.Resolve(typeName, KnownTypes());
        type.Should().NotBeNull($"Тип должен быть зарегистрирован в перегрузке метода {nameof(KnownTypes)}, или глобально в используемом {nameof(TypeResolver)}, чтобы иметь возможность использовать {nameof(GetByTypeName)}({nameof(typeName)})");

        if (Context.TryGetValue(type!.FullName!, out var value)) {
            return value;
        }

        return value ?? Context.Get<IServiceProvider>()?.GetService(type)!;
    }

    protected object GetFromTable(string typeName, Table table) {
        var type = TTypeResolver.Resolve(typeName, KnownTypes());
        type.Should().NotBeNull($"Тип должен быть зарегистрирован в перегрузке метода {nameof(KnownTypes)}, или глобально в используемом {nameof(TypeResolver)}, чтобы иметь возможность использовать {nameof(GetFromTable)}({nameof(typeName)}, {nameof(table)})");

        var typeMapper = Get<PropertyInstanceTypeMapper>();
        return table.To(type!, SerializerOptions, typeMapper.GetInstanceTypeForProperty);
    }

    protected Type? GetTypeByName(string typeName) {
        return TTypeResolver.Resolve(typeName, KnownTypes());
    }

    protected void ShouldBeAsTable(object value, Table table) {
        var typeMapper = Get<PropertyInstanceTypeMapper>();
        value.Should().BeAsTable(table, SerializerOptions, typeMapper.GetInstanceTypeForProperty);
    }

    protected virtual Task Invoke(MethodInfo method, object target, object request) {
        var parameters = method.GetParameters();
        if (parameters.Length == 1)
            return (Task) method.Invoke(target, new[] {request})!;

        if (parameters.Length == 2 && parameters[1].ParameterType == typeof(CancellationToken)) {
            if (method.IsGenericMethod)
                method = MakeSingleParameterGenericMethod(method, request.GetType());

            return (Task) method.Invoke(target, new[] {request, CancellationToken.None})!;
        }

        throw new NotSupportedException($"Can't invoke method {target.GetType().GetNestedName()}.{method.Name}({string.Join(", ", parameters.Select(x => $"{x.ParameterType.GetNestedName()} {x.Name}"))}) with single {request.GetType()} parameter");
    }


    protected virtual MethodInfo FindMethod(Type target, Type requestType, string methodName) {
        var methods = target
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(x => x.Name == methodName)
            .Where(x => {
                var parameters = x.GetParameters();
                return parameters.Length == 1 || (parameters.Length == 2 && parameters[1].ParameterType == typeof(CancellationToken));
            })
            .ToArray();

        methods.Should().NotBeEmpty($"{target.GetNestedName()} should contain public instance method {methods} that takes one parameter of type {requestType.GetNestedName()}, except an {nameof(CancellationToken)}");

        if (methods.Length == 1)
            return methods[0];

        var genericMethod = methods.Where(x => {
            if (!x.IsGenericMethod)
                return false;

            var generics = x.GetGenericArguments();
            return generics.Length == 1 && CanMakeSingleParameterGenericMethod(x, requestType);
        }).FirstOrDefault();
        if (genericMethod != null)
            return genericMethod;

        var method = methods.FirstOrDefault(x => !x.IsGenericMethod);
        method.Should().NotBeNull($"{target.GetNestedName()} should contain public instance method {methods} that takes one parameter of type {requestType.GetNestedName()}, except an {nameof(CancellationToken)}");
        return method!;
    }


    protected virtual MethodInfo MakeSingleParameterGenericMethod(MethodInfo method, Type requestType, Type? genericParameter = null) {
        genericParameter ??= method.GetGenericArguments()[0].BaseType!;
        if (genericParameter.IsAssignableFrom(requestType))
            return method.MakeGenericMethod(requestType);

        if (!requestType.IsGenericType) {
            throw new InvalidOperationException($"Can't make generic method {method.ReflectedType?.GetNestedName()}.{method.Name}<{genericParameter.Name}>() from request of type {requestType.GetNestedName()}");
        }

        return MakeSingleParameterGenericMethod(method, requestType.GetGenericArguments()[0], genericParameter);
    }

    protected virtual bool CanMakeSingleParameterGenericMethod(MethodInfo method, Type requestType, Type? genericParameter = null) {
        genericParameter ??= method.GetGenericArguments()[0].BaseType!;
        if (genericParameter.IsAssignableFrom(requestType))
            return true;

        if (!requestType.IsGenericType)
            return false;

        return CanMakeSingleParameterGenericMethod(method, requestType.GetGenericArguments()[0], genericParameter);
    }
}
