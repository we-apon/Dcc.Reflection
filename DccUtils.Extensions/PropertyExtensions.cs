using System.Reflection;

namespace Dcc.Extensions;

public static class PropertyExtensions {

        public static IEnumerable<PropertyInfo> GetPropertyPath(this object item, string propertyPath, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance) {
            var context = item;
            var currentType = context.GetType();
            foreach (var propName in propertyPath.Split('.')) {
                var property = currentType!.GetProperty(propName, bindingFlags);
                if (property == null) {
                    throw new ArgumentException($"Property {propName} is not found on object of type {currentType.FullName}", nameof(propertyPath));
                }

                yield return property;

                if (context != null) {
                    context = property.GetValue(context);
                }

                if (context != null) {
                    currentType = context.GetType();
                    continue;
                }

                currentType = property.PropertyType;
            }
        }

        public static IEnumerable<PropertyInfo> GetPropertyPath(this Type itemType, string propertyPath, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance) {
            var currentType = itemType;
            foreach (var propName in propertyPath.Split('.')) {
                var property = currentType.GetProperty(propName, bindingFlags);
                if (property == null) {
                    throw new ArgumentException($"Property {propName} is not found on object of type {currentType.FullName}", nameof(propertyPath));
                }

                yield return property;

                currentType = property.PropertyType;
            }
        }

        public static T? GetPropertyValue<T>(this object context, string propertyPath, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance) {
            var value = context.GetPropertyValue(propertyPath, bindingFlags);
            if (value == null)
                return default;

            return (T) value;
        }

        public static object? GetPropertyValue(this object o, string propertyPath, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance) {
            var context = o;
            foreach (var propName in propertyPath.Split('.')) {
                var type = context!.GetType();
                var property = type.GetProperty(propName, bindingFlags);
                if (property == null) {
                    throw new ArgumentException($"Property {propName} is not found on object of type {type.FullName}", nameof(propertyPath));
                }

                context = property.GetValue(context);
                if (context == null) {
                    return null;
                }
            }

            return context;
        }
}
