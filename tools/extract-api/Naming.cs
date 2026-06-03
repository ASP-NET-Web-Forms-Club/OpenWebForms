using Mono.Cecil;
using System.Text;

namespace ExtractApi;

/// <summary>
/// Renders Cecil type/member references into both fully-qualified names (for JSON)
/// and compilable C# (for stubs). Metadata only — never touches IL bodies.
/// </summary>
internal static class Naming
{
    // ---- Fully-qualified name (JSON form): "System.Web.UI.Control", no global:: prefix ----
    public static string Json(TypeReference t) => Render(t, stub: false);

    // ---- Compilable C# form for stubs: "global::System.Web.UI.Control" ----
    public static string Stub(TypeReference t) => Render(t, stub: true);

    private static string Render(TypeReference t, bool stub)
    {
        switch (t)
        {
            case ByReferenceType br:
                // byref handled by callers (ref/out/in); render the element type
                return Render(br.ElementType, stub);
            case ArrayType arr:
                return Render(arr.ElementType, stub) + "[" + new string(',', Math.Max(0, arr.Rank - 1)) + "]";
            case PointerType ptr:
                return Render(ptr.ElementType, stub) + "*";
            case PinnedType pin:
                return Render(pin.ElementType, stub);
            case RequiredModifierType rm:
                return Render(rm.ElementType, stub);
            case OptionalModifierType om:
                return Render(om.ElementType, stub);
            case GenericParameter gp:
                return gp.Name;
            case GenericInstanceType gi:
                return RenderGenericInstance(gi, stub);
            default:
                return Named(t, stub);
        }
    }

    // Renders e.g. Dictionary<string,object>.Enumerator — distributing the flat generic
    // argument list across the declaring chain by each segment's OWN arity (backtick count).
    private static string RenderGenericInstance(GenericInstanceType gi, bool stub)
    {
        var args = gi.GenericArguments;
        var chain = new List<TypeReference>();
        for (TypeReference? cur = gi.ElementType; cur != null; cur = cur.DeclaringType)
            chain.Add(cur);
        chain.Reverse(); // outermost first

        var sb = new StringBuilder();
        int argIdx = 0;
        for (int level = 0; level < chain.Count; level++)
        {
            var seg = chain[level];
            string simple = StripArity(seg.Name);
            if (level == 0)
            {
                string ns = seg.Namespace;
                string full = string.IsNullOrEmpty(ns) ? simple : ns + "." + simple;
                sb.Append(stub ? "global::" + full : full);
            }
            else
            {
                sb.Append('.').Append(simple);
            }

            int own = ArityOf(seg.Name);
            if (own > 0)
            {
                sb.Append('<');
                for (int k = 0; k < own && argIdx < args.Count; k++)
                {
                    if (k > 0) sb.Append(", ");
                    sb.Append(Render(args[argIdx + k], stub));
                }
                sb.Append('>');
                argIdx += own;
            }
        }
        return sb.ToString();
    }

    private static int ArityOf(string name)
    {
        int i = name.IndexOf('`');
        if (i < 0) return 0;
        int j = i + 1, n = 0;
        while (j < name.Length && char.IsDigit(name[j])) { n = n * 10 + (name[j] - '0'); j++; }
        return n;
    }

    // Named type with its own generic parameters expressed as <T0,T1> when it is an open generic definition.
    private static string Named(TypeReference t, bool stub)
    {
        if (t.FullName == "System.Void") return "void";
        string baseName = NamedBase(t, stub);
        if (t.HasGenericParameters)
        {
            var sb = new StringBuilder(baseName);
            sb.Append('<');
            for (int i = 0; i < t.GenericParameters.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(t.GenericParameters[i].Name);
            }
            sb.Append('>');
            return sb.ToString();
        }
        return baseName;
    }

    // Namespace + nested chain, arity backtick stripped, no generic args appended.
    private static string NamedBase(TypeReference t, bool stub)
    {
        string simple = StripArity(t.Name);
        if (t.DeclaringType != null)
            return NamedBase(t.DeclaringType, stub) + "." + simple;
        string ns = t.Namespace;
        string full = string.IsNullOrEmpty(ns) ? simple : ns + "." + simple;
        return stub ? "global::" + full : full;
    }

    public static string StripArity(string name)
    {
        int idx = name.IndexOf('`');
        return idx >= 0 ? name.Substring(0, idx) : name;
    }

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract","as","base","bool","break","byte","case","catch","char","checked","class","const",
        "continue","decimal","default","delegate","do","double","else","enum","event","explicit","extern",
        "false","finally","fixed","float","for","foreach","goto","if","implicit","in","int","interface",
        "internal","is","lock","long","namespace","new","null","object","operator","out","override","params",
        "private","protected","public","readonly","ref","return","sbyte","sealed","short","sizeof","stackalloc",
        "static","string","struct","switch","this","throw","true","try","typeof","uint","ulong","unchecked",
        "unsafe","ushort","using","virtual","void","volatile","while"
    };

    public static string Ident(string name)
    {
        if (string.IsNullOrEmpty(name)) return "_";
        return Keywords.Contains(name) ? "@" + name : name;
    }
}
