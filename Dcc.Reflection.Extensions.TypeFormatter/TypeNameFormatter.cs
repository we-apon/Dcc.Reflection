using System.Text;

namespace Dcc.Reflection.Extensions.TypeFormatter;

public abstract class TypeNameFormatter {
    public abstract string GetTypeName(Type type);

    public virtual char GetNestedNameSplitter() => '.';

    public virtual string GetUniqueTypeName(Type type) => type.FullName!;

    public virtual void FormatGenericDefinitionArgs(StringBuilder builder, IEnumerable<string?> genericArgumentTypeNames) {
        builder.Append('<');

        using var enumerator = genericArgumentTypeNames.GetEnumerator();
        enumerator.MoveNext();
        builder.Append(enumerator.Current);

        while (enumerator.MoveNext()) {
            builder.Append(',').Append(enumerator.Current);
        }

        builder.Append('>');
    }

    public class TypeNameHierarchy {
        public string Name { get; set; } = null!;
        public IList<TypeNameHierarchy> Generics { get; set; } = new List<TypeNameHierarchy>();

        public override string ToString() => $"{Name}{(Generics.Any() ? $"<{string.Join(',', Generics)}>" : string.Empty)}";
    }


    public virtual TypeNameHierarchy GetHierarchy(ref ReadOnlySpan<char> span) {

        var genericIndex = span.IndexOf('<');
        if (genericIndex < 0) {
            return new TypeNameHierarchy {Name = span.Trim().ToString()};
        }

        var hierarchy = new TypeNameHierarchy {
            Name = span[..genericIndex].Trim().ToString()
        };

        var closedGenericIndex = span.LastIndexOf('>');
        span = span.Slice(genericIndex + 1, closedGenericIndex - genericIndex - 1);

        while (true) {
            var length = GetLengthOfNextSubType(ref span);
            if (span.Length == length) {
                hierarchy.Generics.Add(GetHierarchy(ref span));
                return hierarchy;
            }


            var subSpan = span[..length];
            hierarchy.Generics.Add(GetHierarchy(ref subSpan));
            span = span[(length+1)..];
        }

        static int GetLengthOfNextSubType(ref ReadOnlySpan<char> typeName) {
            var nesting = 0;
            for (var i = 0; i < typeName.Length; i++) {
                var chr = typeName[i];

                if (chr == ',' && nesting == 0) {
                    return i;
                }

                if (chr == '<') {
                    nesting++;
                    continue;
                }

                if (chr == '>') {
                    nesting--;
                }
            }

            return typeName.Length;
        }
    }

    public virtual TypeNameHierarchy GetHierarchy(string formattedTypeName) {
        var genericIndex = formattedTypeName.IndexOf('<');
        if (genericIndex < 0) {
            return new TypeNameHierarchy {Name = formattedTypeName.Trim()};
        }

        var hierarchy = new TypeNameHierarchy {
            Name = formattedTypeName.Substring(0, genericIndex).Trim()
        };

        var closedGenericIndex = formattedTypeName.LastIndexOf('>');
        formattedTypeName = formattedTypeName.Substring(genericIndex + 1, closedGenericIndex - genericIndex - 1);


        while (true) {
            var length = GetLengthOfNextSubType(formattedTypeName);
            if (formattedTypeName.Length == length) {
                hierarchy.Generics.Add(GetHierarchy(formattedTypeName));
                return hierarchy;
            }


            hierarchy.Generics.Add(GetHierarchy(formattedTypeName.Substring(0, length)));
            formattedTypeName = formattedTypeName.Substring(length + 1);
        }

        static int GetLengthOfNextSubType(string typeName) {
            var nesting = 0;
            for (var i = 0; i < typeName.Length; i++) {
                var chr = typeName[i];

                if (chr == ',' && nesting == 0) {
                    return i;
                }

                if (chr == '<') {
                    nesting++;
                    continue;
                }

                if (chr == '>') {
                    nesting--;
                }
            }

            return typeName.Length;
        }
    }



    public virtual string GetFormattedName(TypeNameHierarchy hierarchy) => hierarchy.Generics.Count switch {
        0 => hierarchy.Name,
        1 => $"{hierarchy.Name}<>",
        _ => $"{hierarchy.Name}<{new string(',', hierarchy.Generics.Count - 1)}>",
    };
}
