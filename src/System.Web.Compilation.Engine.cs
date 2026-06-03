// Roslyn-backed compile + cache engine for the clean-room BuildManager.
//
// Resolves a virtual path to a physical .aspx/.ascx, parses it (clean-room TemplateParser),
// generates C# (TemplateCodeGenerator), compiles with Roslyn against OUR System.Web + the
// trusted-platform BCL + app bin, and loads the emitted assembly INTO THE SAME
// AssemblyLoadContext as the executing System.Web so the generated page binds to OURS.
//
// NO LINQ (explicit for/foreach). All types internal.
#nullable disable
#pragma warning disable

namespace System.Web.Compilation
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Runtime.Loader;
    using System.Text;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.Emit;
    using Microsoft.CodeAnalysis.Text;
    using SWUI = System.Web.UI;

    internal static class BuildManagerEngine
    {
        private static readonly object s_lock = new object();
        // normalized virtual path -> compiled Type
        private static readonly Dictionary<string, Type> s_typeCache =
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        // normalized virtual path -> generated source (for diagnostics / GetCompiledCustomString)
        private static readonly Dictionary<string, string> s_sourceCache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // Extra app-supplied referenced assemblies (BuildManager.AddReferencedAssembly).
        private static readonly List<Assembly> s_referenced = new List<Assembly>();

        private static List<MetadataReference> s_baseReferences;

        internal static void AddReferencedAssembly(Assembly a)
        {
            if (a == null) { return; }
            lock (s_lock)
            {
                if (!s_referenced.Contains(a)) { s_referenced.Add(a); }
                s_baseReferences = null; // force rebuild to include the new reference
            }
        }

        internal static ICollection GetReferencedAssemblies()
        {
            lock (s_lock)
            {
                return new List<Assembly>(s_referenced).ToArray();
            }
        }

        // ----- public-ish entry points the BuildManager statics delegate to -----

        internal static Type GetCompiledType(string virtualPath)
        {
            if (virtualPath == null) { throw new ArgumentNullException("virtualPath"); }
            string norm = NormalizeVirtualPath(virtualPath);
            lock (s_lock)
            {
                Type cached;
                if (s_typeCache.TryGetValue(norm, out cached)) { return cached; }
            }
            Type t = CompileVirtualPath(norm);
            lock (s_lock)
            {
                s_typeCache[norm] = t;
            }
            return t;
        }

        internal static object CreateInstanceFromVirtualPath(string virtualPath, Type requiredBaseType)
        {
            Type t = GetCompiledType(virtualPath);
            if (t == null) { return null; }
            if (requiredBaseType != null && !requiredBaseType.IsAssignableFrom(t))
            {
                throw new global::System.Web.HttpException(
                    "The type '" + t.FullName + "' compiled from '" + virtualPath +
                    "' does not derive from the required base type '" + requiredBaseType.FullName + "'.");
            }
            return Activator.CreateInstance(t);
        }

        internal static Assembly GetCompiledAssembly(string virtualPath)
        {
            Type t = GetCompiledType(virtualPath);
            return t != null ? t.Assembly : null;
        }

        internal static string GetCompiledCustomString(string virtualPath)
        {
            string norm = NormalizeVirtualPath(virtualPath);
            GetCompiledType(norm);
            lock (s_lock)
            {
                string src;
                if (s_sourceCache.TryGetValue(norm, out src)) { return src; }
            }
            return null;
        }

        // Resolved application (global.asax) type cache. s_globalAsaxResolved distinguishes
        // "not yet looked up" from "looked up, no global.asax present" (s_globalAsaxType == null).
        private static bool s_globalAsaxResolved;
        private static Type s_globalAsaxType;

        // Discover and compile the application file (global.asax) at the app root, returning the
        // compiled HttpApplication-derived type, or null when no global.asax exists. The lookup is
        // case-insensitive over the directory listing so it resolves "Global.asax" on a
        // case-sensitive file system (Linux), where the canonical name is capitalized.
        internal static Type GetGlobalAsaxType()
        {
            lock (s_lock)
            {
                if (s_globalAsaxResolved) { return s_globalAsaxType; }
            }

            string actualName = FindGlobalAsaxFileName();
            Type t = null;
            if (actualName != null)
            {
                t = GetCompiledType("/" + actualName);
            }

            lock (s_lock)
            {
                s_globalAsaxType = t;
                s_globalAsaxResolved = true;
            }
            return t;
        }

        // Test-only: clear the compiled-type, source, and global.asax caches so an isolated test
        // pointing the runtime at a fresh app directory recompiles instead of returning a stale
        // cached type. Internal; does not affect the public surface.
        internal static void ResetCachesForTest()
        {
            lock (s_lock)
            {
                s_typeCache.Clear();
                s_sourceCache.Clear();
                s_globalAsaxResolved = false;
                s_globalAsaxType = null;
            }
        }

        private static string FindGlobalAsaxFileName()
        {
            string appRoot = global::System.Web.HttpRuntime.AppDomainAppPath;
            if (appRoot == null || appRoot.Length == 0) { appRoot = AppContext.BaseDirectory; }
            if (appRoot == null || !Directory.Exists(appRoot)) { return null; }
            string[] files;
            try { files = Directory.GetFiles(appRoot); }
            catch (Exception) { return null; }
            for (int i = 0; i < files.Length; i++)
            {
                string name = Path.GetFileName(files[i]);
                if (string.Equals(name, "global.asax", StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }
            }
            return null;
        }

        // ----- core: parse -> codegen -> compile -> load -----

        private static Type CompileVirtualPath(string normalizedVirtualPath)
        {
            string physical = MapPath(normalizedVirtualPath);
            if (physical == null || !File.Exists(physical))
            {
                throw new global::System.Web.HttpException(404,
                    "The file '" + normalizedVirtualPath + "' does not exist.");
            }

            string ext = Path.GetExtension(physical);
            bool isUserControl = string.Equals(ext, ".ascx", StringComparison.OrdinalIgnoreCase);
            bool isMaster = string.Equals(ext, ".master", StringComparison.OrdinalIgnoreCase);
            bool isApplication = string.Equals(ext, ".asax", StringComparison.OrdinalIgnoreCase);

            string text = File.ReadAllText(physical);

            SWUI.TemplateParser parser;
            if (isApplication)
            {
                SWUI.ApplicationFileParser afp = new SWUI.ApplicationFileParser();
                afp.CurrentVirtualPath = normalizedVirtualPath;
                afp.ParseString(text);
                parser = afp;
            }
            else if (isMaster)
            {
                SWUI.MasterPageParser mpp = new SWUI.MasterPageParser();
                mpp.CurrentVirtualPath = normalizedVirtualPath;
                mpp.ParseString(text);
                parser = mpp;
            }
            else if (isUserControl)
            {
                SWUI.UserControlParser ucp = new SWUI.UserControlParser();
                ucp.CurrentVirtualPath = normalizedVirtualPath;
                ucp.ParseString(text);
                parser = ucp;
            }
            else
            {
                SWUI.PageParser pp = new SWUI.PageParser();
                pp.ParsePageText(text, normalizedVirtualPath);
                parser = pp;
            }


            // Surface parse errors as HttpParseException.
            List<global::System.Web.ParserError> errs = parser.ParseErrors;
            if (errs != null && errs.Count != 0)
            {
                global::System.Web.ParserError first = errs[0];
                throw new global::System.Web.HttpParseException(
                    first.ErrorText, null, normalizedVirtualPath, parser.Text, first.Line);
            }

            // A master compiles like a user control (FrameworkInitialize builds the tree, no Page
            // AutoEventWireup), but derives from MasterPage via the parser's DefaultBaseType.
            // global.asax compiles to a plain HttpApplication-derived class (no control tree).
            TemplateCodeGenerator gen = new TemplateCodeGenerator(parser, normalizedVirtualPath, isUserControl || isMaster, isApplication);
            string source = gen.Generate();

            lock (s_lock) { s_sourceCache[normalizedVirtualPath] = source; }

            // Optional code-behind file.
            List<string> sources = new List<string>();
            sources.Add(source);
            string codeFile = parser.CodeFileName;
            if (codeFile != null && codeFile.Length != 0)
            {
                string cbPhysical = ResolveCodeFile(physical, normalizedVirtualPath, codeFile);
                if (cbPhysical != null && File.Exists(cbPhysical))
                {
                    sources.Add(File.ReadAllText(cbPhysical));
                }
            }

            Assembly compiled = CompileSources(sources, normalizedVirtualPath, source, parser);

            // Find the generated type ASP.<ClassName>.
            string typeName = "ASP." + gen.GeneratedClassName;
            Type t = compiled.GetType(typeName, false, false);
            if (t == null)
            {
                // Fall back: first public type deriving from the resolved base.
                Type[] types = compiled.GetTypes();
                for (int i = 0; i < types.Length; i++)
                {
                    if (gen.ResolvedBaseType != null && gen.ResolvedBaseType.IsAssignableFrom(types[i]))
                    {
                        t = types[i];
                        break;
                    }
                }
            }
            if (t == null)
            {
                throw new global::System.Web.HttpException(
                    "Could not locate the generated type '" + typeName + "' in the compiled assembly.");
            }
            return t;
        }

        private static Assembly CompileSources(List<string> sources, string virtualPath, string primarySource, SWUI.TemplateParser parser)
        {
            List<SyntaxTree> trees = new List<SyntaxTree>();
            CSharpParseOptions parseOpts = new CSharpParseOptions(LanguageVersion.Latest);
            for (int i = 0; i < sources.Count; i++)
            {
                trees.Add(CSharpSyntaxTree.ParseText(SourceText.From(sources[i], Encoding.UTF8), parseOpts));
            }

            List<MetadataReference> refs = BuildReferences(parser);

            string asmName = "App_Web_" + Guid.NewGuid().ToString("N");
            CSharpCompilationOptions opts = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Debug,
                allowUnsafe: true);

            CSharpCompilation compilation = CSharpCompilation.Create(asmName, trees, refs, opts);

            using (MemoryStream ms = new MemoryStream())
            {
                EmitResult result = compilation.Emit(ms);
                if (!result.Success)
                {
                    throw BuildCompileException(result, primarySource, virtualPath);
                }
                ms.Seek(0, SeekOrigin.Begin);
                AssemblyLoadContext alc = AssemblyLoadContext.GetLoadContext(typeof(BuildManager).Assembly);
                if (alc == null) { alc = AssemblyLoadContext.Default; }
                return alc.LoadFromStream(ms);
            }
        }

        private static global::System.Web.HttpCompileException BuildCompileException(EmitResult result, string source, string virtualPath)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Compilation failed for '").Append(virtualPath).Append("':\r\n");
            ImmutableArrayDiagnosticWalk(result, sb);
            return new global::System.Web.HttpCompileException(sb.ToString(), null);
        }

        private static void ImmutableArrayDiagnosticWalk(EmitResult result, StringBuilder sb)
        {
            foreach (Diagnostic d in result.Diagnostics)
            {
                if (d.Severity == DiagnosticSeverity.Error)
                {
                    sb.Append(d.Id).Append(": ").Append(d.GetMessage())
                      .Append(" @ ").Append(d.Location.GetLineSpan().ToString()).Append("\r\n");
                }
            }
        }

        private static List<MetadataReference> BuildReferences(SWUI.TemplateParser parser)
        {
            List<MetadataReference> refs;
            lock (s_lock)
            {
                if (s_baseReferences == null) { s_baseReferences = ComputeBaseReferences(); }
                refs = new List<MetadataReference>(s_baseReferences);
            }

            // @Assembly directives + app bin: resolve by simple name to a loaded assembly location.
            if (parser != null)
            {
                List<string> asmNames = parser.AssembliesInternal;
                if (asmNames != null)
                {
                    for (int i = 0; i < asmNames.Count; i++)
                    {
                        Assembly a = TryLoadAssembly(asmNames[i]);
                        if (a != null) { AddRefForAssembly(refs, a); }
                    }
                }
            }
            return refs;
        }

        private static List<MetadataReference> ComputeBaseReferences()
        {
            List<MetadataReference> refs = new List<MetadataReference>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Trusted platform assemblies (the BCL) come from the AppContext switch.
            object tpaObj = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            string tpa = tpaObj as string;
            if (tpa != null && tpa.Length != 0)
            {
                string[] parts = tpa.Split(Path.PathSeparator);
                for (int i = 0; i < parts.Length; i++)
                {
                    string p = parts[i];
                    if (p == null || p.Length == 0) { continue; }
                    // Skip the shared-framework System.Web facade; we add OURS explicitly below.
                    string fileName = Path.GetFileName(p);
                    if (string.Equals(fileName, "System.Web.dll", StringComparison.OrdinalIgnoreCase)) { continue; }
                    if (seen.Add(p))
                    {
                        try { refs.Add(MetadataReference.CreateFromFile(p)); } catch (Exception) { }
                    }
                }
            }

            // OUR System.Web.dll (the executing one).
            Assembly sw = typeof(BuildManager).Assembly;
            AddRefForAssembly(refs, sw);

            // App-supplied references.
            for (int i = 0; i < s_referenced.Count; i++) { AddRefForAssembly(refs, s_referenced[i]); }

            return refs;
        }

        private static void AddRefForAssembly(List<MetadataReference> refs, Assembly a)
        {
            if (a == null) { return; }
            try
            {
                string loc = a.Location;
                if (loc != null && loc.Length != 0 && File.Exists(loc))
                {
                    for (int i = 0; i < refs.Count; i++)
                    {
                        PortableExecutableReference per = refs[i] as PortableExecutableReference;
                        if (per != null && string.Equals(per.FilePath, loc, StringComparison.OrdinalIgnoreCase)) { return; }
                    }
                    refs.Add(MetadataReference.CreateFromFile(loc));
                }
            }
            catch (Exception) { }
        }

        private static Assembly TryLoadAssembly(string name)
        {
            if (name == null || name.Length == 0) { return null; }
            try { return Assembly.Load(new AssemblyName(name)); }
            catch (Exception) { }
            Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < loaded.Length; i++)
            {
                if (string.Equals(loaded[i].GetName().Name, name, StringComparison.OrdinalIgnoreCase)) { return loaded[i]; }
            }
            return null;
        }

        // ----- path resolution -----

        internal static string NormalizeVirtualPath(string virtualPath)
        {
            if (virtualPath == null) { return "/"; }
            string vp = virtualPath.Replace('\\', '/');
            if (vp.StartsWith("~/")) { vp = vp.Substring(1); }            // ~/x -> /x
            else if (vp.StartsWith("~")) { vp = "/" + vp.Substring(1); }
            if (!vp.StartsWith("/")) { vp = "/" + vp; }
            return vp;
        }

        // Resolve a normalized virtual path ("/foo/bar.aspx") to a physical path under the app root.
        internal static string MapPath(string normalizedVirtualPath)
        {
            string appRoot = global::System.Web.HttpRuntime.AppDomainAppPath;
            if (appRoot == null || appRoot.Length == 0) { appRoot = AppContext.BaseDirectory; }
            string appVirtual = global::System.Web.HttpRuntime.AppDomainAppVirtualPath;
            if (appVirtual == null || appVirtual.Length == 0) { appVirtual = "/"; }

            string rel = normalizedVirtualPath;
            // Strip the application virtual path prefix if present.
            if (appVirtual.Length > 1 && rel.StartsWith(appVirtual, StringComparison.OrdinalIgnoreCase))
            {
                rel = rel.Substring(appVirtual.Length);
            }
            if (rel.StartsWith("/")) { rel = rel.Substring(1); }
            rel = rel.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(appRoot, rel));
        }

        private static string ResolveCodeFile(string aspxPhysical, string virtualPath, string codeFile)
        {
            // CodeFile/CodeBehind/Src may be app-relative (~/...) or relative to the .aspx folder.
            string cf = codeFile.Replace('\\', '/');
            if (cf.StartsWith("~/") || cf.StartsWith("/"))
            {
                return MapPath(NormalizeVirtualPath(cf));
            }
            string dir = Path.GetDirectoryName(aspxPhysical);
            if (dir == null) { dir = global::System.Web.HttpRuntime.AppDomainAppPath; }
            return Path.GetFullPath(Path.Combine(dir, cf.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}