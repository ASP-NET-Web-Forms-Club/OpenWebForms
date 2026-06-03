using Mono.Cecil;

namespace ExtractApi;

internal static class Model
{
    public static string KindOf(TypeDefinition t)
    {
        if (t.IsEnum) return "enum";
        if (t.IsInterface) return "interface";
        if (IsDelegate(t)) return "delegate";
        if (t.IsValueType) return "struct";
        return "class";
    }

    public static bool IsDelegate(TypeDefinition t)
    {
        var bt = t.BaseType;
        return bt != null && (bt.FullName == "System.MulticastDelegate" || bt.FullName == "System.Delegate");
    }

    // Display name with generic arity rendered as <T0,T1> (used in JSON "name").
    public static string DisplayName(TypeDefinition t)
    {
        string simple = Naming.StripArity(t.Name);
        if (!t.HasGenericParameters) return simple;
        return simple + "<" + string.Join(", ", t.GenericParameters.Select(g => g.Name)) + ">";
    }

    public static string TypeAccessibility(TypeDefinition t)
    {
        if (t.IsNested)
        {
            if (t.IsNestedPublic) return "public";
            if (t.IsNestedFamily) return "protected";
            if (t.IsNestedFamilyOrAssembly) return "protected internal";
            if (t.IsNestedFamilyAndAssembly) return "private protected";
            return "internal";
        }
        return t.IsPublic ? "public" : "internal";
    }

    public static string BaseTypeForJson(TypeDefinition t)
    {
        if (t.IsInterface || t.IsEnum || t.IsValueType || IsDelegate(t)) return "";
        var bt = t.BaseType;
        if (bt == null || bt.FullName == "System.Object") return "";
        return Naming.Json(bt);
    }

    // Methods that are not ctors and not property/event accessors.
    public static bool IsRealMethod(MethodDefinition m)
    {
        if (m.IsConstructor) return false;
        if (m.IsGetter || m.IsSetter || m.IsAddOn || m.IsRemoveOn || m.IsFire) return false;
        if (m.Name == "Finalize" && m.Parameters.Count == 0) return false; // destructor, not API surface
        return true;
    }

    // Visible == part of the externally-observable surface. An explicit interface impl counts
    // only when the implemented interface is itself public — an explicit impl of an *internal*
    // interface is effectively a private member (the interface cannot be named by consumers).
    public static bool IsMemberVisible(MethodDefinition m)
    {
        if (IsExplicitImpl(m)) return IsVisibleInterface(m.Overrides[0].DeclaringType);
        return m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly || m.IsFamilyAndAssembly;
    }

    public static bool IsExplicitImpl(MethodDefinition m)
        => m.HasOverrides && m.IsPrivate;

    private static readonly Dictionary<string, bool> _ifaceVisibleCache = new(StringComparer.Ordinal);
    public static bool IsVisibleInterface(TypeReference itf)
    {
        string key = itf.FullName;
        if (_ifaceVisibleCache.TryGetValue(key, out var cached)) return cached;
        TypeDefinition? d;
        try { d = itf.Resolve(); } catch { d = null; }
        bool visible = d == null || Program.IsVisible(d); // null => external, assume public
        _ifaceVisibleCache[key] = visible;
        return visible;
    }

    public static bool IsFieldVisible(FieldDefinition f)
    {
        if (f.IsSpecialName && f.Name == "value__") return false; // enum backing
        return f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly || f.IsFamilyAndAssembly;
    }

    public static string MemberAccessibility(MethodDefinition m)
    {
        if (m.IsPublic) return "public";
        if (m.IsFamily) return "protected";
        if (m.IsFamilyOrAssembly) return "protected internal";
        if (m.IsFamilyAndAssembly) return "private protected";
        if (m.IsPrivate) return "private";
        return "internal";
    }

    public static string FieldAccessibility(FieldDefinition f)
    {
        if (f.IsPublic) return "public";
        if (f.IsFamily) return "protected";
        if (f.IsFamilyOrAssembly) return "protected internal";
        if (f.IsFamilyAndAssembly) return "private protected";
        if (f.IsPrivate) return "private";
        return "internal";
    }

    public static string? ParamModifier(ParameterDefinition p)
    {
        if (p.ParameterType is ByReferenceType brt)
        {
            if (p.IsOut) return "out";
            // 'in' = readonly byref input
            if (p.IsIn && brt is ByReferenceType) return "in";
            return "ref";
        }
        if (IsParamArray(p)) return "params";
        return null;
    }

    public static bool IsParamArray(ParameterDefinition p)
        => p.HasCustomAttributes && p.CustomAttributes.Any(a => a.AttributeType.FullName == "System.ParamArrayAttribute");

    private static readonly HashSet<string> Notable = new(StringComparer.Ordinal)
    {
        "System.SerializableAttribute",
        "System.FlagsAttribute",
        "System.AttributeUsageAttribute",
        "System.ComponentModel.DefaultPropertyAttribute",
        "System.ComponentModel.DefaultEventAttribute",
        "System.Web.UI.ParseChildrenAttribute",
        "System.Web.UI.ControlBuilderAttribute",
        "System.Web.UI.PersistChildrenAttribute",
        "System.Web.UI.ToolboxDataAttribute",
        "System.ComponentModel.TypeConverterAttribute",
        "System.ComponentModel.DesignerAttribute",
        "System.ObsoleteAttribute",
    };

    public static IEnumerable<string> NotableAttributes(TypeDefinition t)
    {
        // [Serializable] is encoded as a metadata flag, not a CustomAttribute.
        if (t.IsSerializable) yield return "Serializable";
        if (!t.HasCustomAttributes) yield break;
        foreach (var a in t.CustomAttributes)
        {
            if (Notable.Contains(a.AttributeType.FullName))
                yield return Naming.StripArity(a.AttributeType.Name);
        }
    }
}
