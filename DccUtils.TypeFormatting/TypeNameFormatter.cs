using System.Text;
using LinkDotNet.StringBuilder;

namespace Dcc.Reflection.TypeFormatting;

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

    public virtual void AppendGenericDefinitionArgs(ref ValueStringBuilder builder, Span<Type?> genericArgumentTypes) {
        builder.Append('<');

        var enumerator = genericArgumentTypes.GetEnumerator();
        enumerator.MoveNext();
        enumerator.Current?.InsertNestedName(ref builder, builder.Length, this);

        while (enumerator.MoveNext()) {
            builder.Append(',');
            enumerator.Current?.InsertNestedName(ref builder, builder.Length, this);
        }

        builder.Append('>');
    }

    public virtual int InsertGenericDefinitionArgs(ref ValueStringBuilder builder, int index, Span<Type?> genericArgumentTypes) {
        builder.Insert(index++, '<');

        var enumerator = genericArgumentTypes.GetEnumerator();
        enumerator.MoveNext();

        if (enumerator.Current != null)
            index = enumerator.Current.InsertNestedName(ref builder, index, this);

        while (enumerator.MoveNext()) {
            builder.Insert(index++, ',');

            if (enumerator.Current != null)
                index = enumerator.Current.InsertNestedName(ref builder, index, this);
        }

        builder.Insert(index++, '>');
        return index;
    }



    public class TypeNameHierarchy {
        public string Name { get; set; } = null!;
        public IList<TypeNameHierarchy> Generics { get; set; } = new List<TypeNameHierarchy>();

        public TypeNameHierarchy? Nested { get; set; }

        public override string ToString() => $"{Name}{(Generics.Any() ? $"<{string.Join(',', Generics)}>" : string.Empty)}";
    }


    public virtual TypeNameHierarchy GetHierarchy(ReadOnlySpan<char> span) {
        var genericIndex = span.IndexOf('<');
        if (genericIndex < 0) {
            return new() {Name = span.ToString()};
        }

        var hierarchy = new TypeNameHierarchy {
            Name = span[..genericIndex].Trim().ToString()
        };

        //todo: тут надо делать нормальную рекурсию по женерикам
        var closedGenericIndex = span.LastIndexOf('>'); //bug: работает только если в nested-типе один женерик

        var nestedSpan = span[(closedGenericIndex + 1)..].Trim('.');
        if (nestedSpan.Length != 0) {
            hierarchy.Nested = GetHierarchy(nestedSpan);
        }

        var genericSpan = span.Slice(genericIndex + 1, closedGenericIndex - genericIndex - 1);

        while (true) {
            var length = GetLengthOfNextSubType(genericSpan);
            if (genericSpan.Length == length) {
                hierarchy.Generics.Add(GetHierarchy(genericSpan));
                return hierarchy;
            }


            var subSpan = genericSpan[..length];
            hierarchy.Generics.Add(GetHierarchy(subSpan));
            genericSpan = genericSpan[(length+1)..];
        }



        static int GetLengthOfNextSubType(ReadOnlySpan<char> typeName) {
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
        1 => hierarchy.Nested == null || hierarchy.Generics.Any(x => !string.IsNullOrWhiteSpace(x.Name)) ? $"{hierarchy.Name}<>" : $"{hierarchy.Name}<>.{GetFormattedName(hierarchy.Nested)}",
        _ => hierarchy.Nested == null || hierarchy.Generics.Any(x => !string.IsNullOrWhiteSpace(x.Name)) ? $"{hierarchy.Name}<{new string(',', hierarchy.Generics.Count - 1)}>" : $"{hierarchy.Name}<{new string(',', hierarchy.Generics.Count - 1)}>.{GetFormattedName(hierarchy.Nested)}",
    };
}
