using System.Reflection;
using Dcc.Reflection.TypeFormatting;
using FluentAssertions;

namespace Dcc.SpecFlow;

public class PropertyInstanceTypeMapper {
    readonly Dictionary<PropertyInfo, Type> _mapping = new();

    public void Map(PropertyInfo prop, Type type) {
        _mapping.TryAdd(prop, type)
            .Should().BeTrue($"Mapping of concrete property to specific type should be done exactly once during single scenario, " +
                             $"but property {prop.PropertyType.GetNestedName()} {(prop.ReflectedType ?? prop.DeclaringType)?.GetNestedName()}.{prop.Name} is already mapped to type {_mapping[prop].GetNestedName()}.\n" +
                             $"Failed to map this property to type {type.GetNestedName()}");
    }

    public Type GetInstanceTypeForProperty(PropertyInfo prop) {
        return _mapping.TryGetValue(prop, out var mappedType)
            ? mappedType
            : prop.PropertyType;
    }
}
