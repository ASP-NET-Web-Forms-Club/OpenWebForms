using Mono.Cecil;
using System.Text;
using System.Text.Json;

namespace ExtractApi;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: extract-api <assembly.dll> <outDir> [--stub-namespace-dir <dir>] [--searchdir <dir>]...");
            return 2;
        }

        string asmPath = args[0];
        string outDir = args[1];
        string? stubDir = null;
        var searchDirs = new List<string>();
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--stub-namespace-dir" && i + 1 < args.Length) { stubDir = args[++i]; }
            else if (args[i] == "--searchdir" && i + 1 < args.Length) { searchDirs.Add(args[++i]); }
        }

        Directory.CreateDirectory(outDir);

        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(asmPath)!);
        foreach (var d in searchDirs) resolver.AddSearchDirectory(d);

        var readerParams = new ReaderParameters
        {
            ReadingMode = ReadingMode.Deferred,
            AssemblyResolver = resolver,
            ReadSymbols = false,
            InMemory = true,
        };

        using var asm = AssemblyDefinition.ReadAssembly(asmPath, readerParams);
        var module = asm.MainModule;

        // ---- Identity verification (printed; the task wants this confirmed) ----
        var name = asm.Name;
        string pkt = name.PublicKeyToken is { Length: > 0 } b
            ? string.Concat(b.Select(x => x.ToString("x2")))
            : "(none)";
        Console.WriteLine($"Assembly : {name.Name}");
        Console.WriteLine($"Version  : {name.Version}");
        Console.WriteLine($"PKT      : {pkt}");
        Console.WriteLine($"Module   : {module.Name}");

        // ---- Collect public/protected types (recursing into nested) in metadata order ----
        var types = new List<TypeDefinition>();
        foreach (var t in module.Types)
            CollectVisible(t, types);

        // ---- Emit JSON ----
        string jsonPath = Path.Combine(outDir, "system.web.api.json");
        EmitJson(asm, module, types, jsonPath);
        Console.WriteLine($"JSON     : {jsonPath} ({types.Count} types)");

        // ---- Emit dependency list (external assembly references) ----
        string depPath = Path.Combine(outDir, "dependencies.txt");
        static string Tok(byte[]? p) => p is { Length: > 0 } ? string.Concat(p.Select(x => x.ToString("x2"))) : "null";
        var deps = module.AssemblyReferences
            .Select(r => $"{r.Name}, Version={r.Version}, PKT={Tok(r.PublicKeyToken)}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
        File.WriteAllText(depPath, string.Join("\n", deps) + "\n");
        Console.WriteLine($"Deps     : {depPath} ({deps.Count} assembly references)");

        // ---- Emit C# stubs ----
        if (stubDir != null)
        {
            Directory.CreateDirectory(stubDir);
            int fileCount = StubEmitter.Emit(types, stubDir);
            Console.WriteLine($"Stubs    : {stubDir} ({fileCount} files)");
        }

        // ---- Counts for quick reporting ----
        ReportCounts(types);
        return 0;
    }

    private static void CollectVisible(TypeDefinition t, List<TypeDefinition> sink)
    {
        if (!IsVisible(t)) return;
        sink.Add(t);
        foreach (var nt in t.NestedTypes)
            CollectVisible(nt, sink);
    }

    // public/protected surface only. Nested internal/private excluded.
    public static bool IsVisible(TypeDefinition t)
    {
        if (t.IsNested)
            return t.IsNestedPublic || t.IsNestedFamily || t.IsNestedFamilyOrAssembly || t.IsNestedFamilyAndAssembly;
        return t.IsPublic;
    }

    private static void ReportCounts(List<TypeDefinition> types)
    {
        int cls = 0, str = 0, iface = 0, en = 0, del = 0;
        foreach (var t in types)
        {
            switch (Model.KindOf(t))
            {
                case "class": cls++; break;
                case "struct": str++; break;
                case "interface": iface++; break;
                case "enum": en++; break;
                case "delegate": del++; break;
            }
        }
        Console.WriteLine($"Kinds    : class={cls} struct={str} interface={iface} enum={en} delegate={del}");
        Console.WriteLine($"Namespaces: {types.Select(t => t.Namespace).Distinct().Count()}");
    }

    // ============================ JSON EMISSION ============================
    private static void EmitJson(AssemblyDefinition asm, ModuleDefinition module, List<TypeDefinition> types, string path)
    {
        using var fs = File.Create(path);
        var opts = new JsonWriterOptions { Indented = true };
        using var w = new Utf8JsonWriter(fs, opts);

        string pkt = asm.Name.PublicKeyToken is { Length: > 0 } b
            ? string.Concat(b.Select(x => x.ToString("x2"))) : "";

        w.WriteStartObject();
        w.WriteString("assembly", asm.Name.Name);
        w.WriteString("version", asm.Name.Version.ToString());
        w.WriteString("frameworkTarget", ".NET Framework 4.8");
        w.WriteString("publicKeyToken", pkt);
        w.WriteStartArray("types");
        foreach (var t in types) WriteType(w, t);
        w.WriteEndArray();
        w.WriteEndObject();
        w.Flush();
    }

    private static void WriteType(Utf8JsonWriter w, TypeDefinition t)
    {
        w.WriteStartObject();
        w.WriteString("namespace", t.Namespace ?? "");
        w.WriteString("name", Model.DisplayName(t));
        w.WriteString("kind", Model.KindOf(t));
        w.WriteString("accessibility", Model.TypeAccessibility(t));
        w.WriteBoolean("isAbstract", t.IsAbstract && !t.IsSealed && !t.IsInterface);
        w.WriteBoolean("isSealed", t.IsSealed && !(t.IsAbstract && t.IsSealed));
        w.WriteBoolean("isStatic", t.IsAbstract && t.IsSealed && !t.IsInterface && !t.IsEnum);
        w.WriteString("baseType", Model.BaseTypeForJson(t));

        w.WriteStartArray("interfaces");
        foreach (var i in t.Interfaces) w.WriteStringValue(Naming.Json(i.InterfaceType));
        w.WriteEndArray();

        w.WriteStartArray("genericParameters");
        foreach (var gp in t.GenericParameters) WriteGenericParam(w, gp);
        w.WriteEndArray();

        w.WriteStartArray("attributes");
        foreach (var a in Model.NotableAttributes(t)) w.WriteStringValue(a);
        w.WriteEndArray();

        // Constructors
        w.WriteStartArray("constructors");
        foreach (var m in t.Methods.Where(m => m.IsConstructor && !m.IsStatic && Model.IsMemberVisible(m)))
        {
            w.WriteStartObject();
            w.WriteString("accessibility", Model.MemberAccessibility(m));
            WriteParams(w, m);
            w.WriteEndObject();
        }
        w.WriteEndArray();

        // Methods (excluding ctors and accessor methods)
        w.WriteStartArray("methods");
        foreach (var m in t.Methods.Where(Model.IsRealMethod).Where(Model.IsMemberVisible))
        {
            w.WriteStartObject();
            w.WriteString("name", m.Name);
            w.WriteString("accessibility", Model.MemberAccessibility(m));
            w.WriteBoolean("isStatic", m.IsStatic);
            w.WriteBoolean("isVirtual", m.IsVirtual && !m.IsFinal);
            w.WriteBoolean("isAbstract", m.IsAbstract);
            w.WriteBoolean("isOverride", m.IsVirtual && !m.IsNewSlot);
            w.WriteString("returnType", Naming.Json(m.ReturnType));
            w.WriteStartArray("genericParameters");
            foreach (var gp in m.GenericParameters) WriteGenericParam(w, gp);
            w.WriteEndArray();
            WriteParams(w, m);
            w.WriteEndObject();
        }
        w.WriteEndArray();

        // Properties
        w.WriteStartArray("properties");
        foreach (var p in t.Properties)
        {
            var acc = p.GetMethod ?? p.SetMethod;
            if (acc == null || !Model.IsMemberVisible(acc)) continue;
            w.WriteStartObject();
            w.WriteString("name", p.Name);
            w.WriteString("type", Naming.Json(p.PropertyType));
            w.WriteString("getter", p.GetMethod != null && Model.IsMemberVisible(p.GetMethod) ? Model.MemberAccessibility(p.GetMethod) : null);
            w.WriteString("setter", p.SetMethod != null && Model.IsMemberVisible(p.SetMethod) ? Model.MemberAccessibility(p.SetMethod) : null);
            w.WriteBoolean("isStatic", acc.IsStatic);
            w.WriteStartArray("indexerParameters");
            foreach (var par in p.Parameters) WriteParam(w, par);
            w.WriteEndArray();
            w.WriteEndObject();
        }
        w.WriteEndArray();

        // Events
        w.WriteStartArray("events");
        foreach (var e in t.Events)
        {
            var acc = e.AddMethod ?? e.RemoveMethod;
            if (acc == null || !Model.IsMemberVisible(acc)) continue;
            w.WriteStartObject();
            w.WriteString("name", e.Name);
            w.WriteString("handlerType", Naming.Json(e.EventType));
            w.WriteString("accessibility", Model.MemberAccessibility(acc));
            w.WriteEndObject();
        }
        w.WriteEndArray();

        // Fields (enum members included for enums)
        w.WriteStartArray("fields");
        foreach (var f in t.Fields.Where(Model.IsFieldVisible))
        {
            w.WriteStartObject();
            w.WriteString("name", f.Name);
            w.WriteString("type", Naming.Json(f.FieldType));
            w.WriteString("accessibility", Model.FieldAccessibility(f));
            w.WriteBoolean("isStatic", f.IsStatic);
            w.WriteBoolean("isConst", f.IsLiteral);
            w.WriteBoolean("isReadonly", f.IsInitOnly);
            if (f.HasConstant)
                w.WriteString("constantValue", Convert.ToString(f.Constant, System.Globalization.CultureInfo.InvariantCulture));
            w.WriteEndObject();
        }
        w.WriteEndArray();

        w.WriteEndObject();
    }

    private static void WriteGenericParam(Utf8JsonWriter w, GenericParameter gp)
    {
        w.WriteStartObject();
        w.WriteString("name", gp.Name);
        var cons = new List<string>();
        if (gp.HasReferenceTypeConstraint) cons.Add("class");
        if (gp.HasNotNullableValueTypeConstraint) cons.Add("struct");
        foreach (var c in gp.Constraints)
        {
            var ct = c.ConstraintType;
            if (ct.FullName == "System.ValueType") continue;
            cons.Add(Naming.Json(ct));
        }
        if (gp.HasDefaultConstructorConstraint && !gp.HasNotNullableValueTypeConstraint) cons.Add("new()");
        w.WriteStartArray("constraints");
        foreach (var c in cons) w.WriteStringValue(c);
        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static void WriteParams(Utf8JsonWriter w, MethodDefinition m)
    {
        w.WriteStartArray("parameters");
        foreach (var p in m.Parameters) WriteParam(w, p);
        w.WriteEndArray();
    }

    private static void WriteParam(Utf8JsonWriter w, ParameterDefinition p)
    {
        w.WriteStartObject();
        w.WriteString("name", p.Name);
        w.WriteString("type", Naming.Json(p.ParameterType));
        w.WriteString("modifier", Model.ParamModifier(p));
        if (p.HasConstant)
            w.WriteString("defaultValue", Convert.ToString(p.Constant, System.Globalization.CultureInfo.InvariantCulture) ?? "null");
        else
            w.WriteNull("defaultValue");
        w.WriteEndObject();
    }
}
