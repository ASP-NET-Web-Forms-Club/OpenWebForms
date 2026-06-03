using Mono.Cecil;

static bool IsVisible(TypeDefinition t)
{
    if (t.IsNested)
        return t.IsNestedPublic || t.IsNestedFamily || t.IsNestedFamilyOrAssembly || t.IsNestedFamilyAndAssembly;
    return t.IsPublic;
}

// Canonical full name: uses Cecil FullName but with nested separator '+' normalized.
static string FullName(TypeDefinition t) => t.FullName;

static void Collect(TypeDefinition t, List<TypeDefinition> sink)
{
    if (!IsVisible(t)) return;
    sink.Add(t);
    foreach (var nt in t.NestedTypes) Collect(nt, sink);
}

static HashSet<string> Load(string path, List<string>? searchDirs = null)
{
    var resolver = new DefaultAssemblyResolver();
    resolver.AddSearchDirectory(Path.GetDirectoryName(path)!);
    if (searchDirs != null) foreach (var d in searchDirs) resolver.AddSearchDirectory(d);
    var rp = new ReaderParameters { ReadingMode = ReadingMode.Deferred, AssemblyResolver = resolver, InMemory = true };
    using var asm = AssemblyDefinition.ReadAssembly(path, rp);
    var list = new List<TypeDefinition>();
    foreach (var t in asm.MainModule.Types) Collect(t, list);
    var set = new HashSet<string>(StringComparer.Ordinal);
    foreach (var t in list)
    {
        if (t.Name == "<Module>") continue;
        set.Add(FullName(t));
    }
    return set;
}

string runtime = args[0];
string refasm = args[1];
string ours = args[2];
string oursSearch = Path.GetDirectoryName(ours)!;

var rt = Load(runtime);
var rf = Load(refasm);
var ou = Load(ours, new List<string> { oursSearch });

Console.WriteLine($"runtime types : {rt.Count}");
Console.WriteLine($"ref types     : {rf.Count}");
Console.WriteLine($"our types     : {ou.Count}");

// runtime vs ref
var onlyRt = new SortedSet<string>(StringComparer.Ordinal); foreach (var x in rt) if (!rf.Contains(x)) onlyRt.Add(x);
var onlyRf = new SortedSet<string>(StringComparer.Ordinal); foreach (var x in rf) if (!rt.Contains(x)) onlyRf.Add(x);
Console.WriteLine($"\n=== runtime-only (not in ref) [{onlyRt.Count}] ===");
foreach (var x in onlyRt) Console.WriteLine("  RT-ONLY " + x);
Console.WriteLine($"\n=== ref-only (not in runtime) [{onlyRf.Count}] ===");
foreach (var x in onlyRf) Console.WriteLine("  RF-ONLY " + x);

// Use runtime as authoritative (superset typically). Real = union of rt and rf.
var real = new HashSet<string>(rt, StringComparer.Ordinal); foreach (var x in rf) real.Add(x);

var missing = new SortedSet<string>(StringComparer.Ordinal); foreach (var x in real) if (!ou.Contains(x)) missing.Add(x);
// extras: ours not in real, ignoring internal cshttp namespace
var extra = new SortedSet<string>(StringComparer.Ordinal);
foreach (var x in ou) if (!real.Contains(x)) extra.Add(x);

Console.WriteLine($"\n=== MISSING (real - ours) [{missing.Count}] ===");
foreach (var x in missing) Console.WriteLine("  MISSING " + x);
Console.WriteLine($"\n=== EXTRA (ours - real) [{extra.Count}] ===");
foreach (var x in extra) Console.WriteLine("  EXTRA " + x);

Console.WriteLine($"\nSUMMARY realUnion={real.Count} ours={ou.Count} missing={missing.Count} extra={extra.Count}");
