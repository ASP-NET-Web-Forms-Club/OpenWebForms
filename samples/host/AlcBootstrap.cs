// Custom AssemblyLoadContext that makes the standalone host load OUR app-local clean-room
// System.Web.dll instead of the shared-framework throw-only System.Web facade.
//
// WHY THIS IS NEEDED
// ------------------
// Microsoft.NETCore.App ships its own strong-named System.Web.dll (AssemblyVersion 4.0.0.0) that
// only type-forwards HttpUtility. It is on the Trusted Platform Assemblies (TPA) list, so in the
// DEFAULT AssemblyLoadContext a bare reference to System.Web binds to that facade and any of our
// types (HttpContext, HttpRuntime, ...) throw. Our clean-room assembly keeps the SAME identity
// (4.0.0.0), so a version bump cannot disambiguate them and is not used.
//
// The fix: load our assembly through a custom ALC whose Load(AssemblyName) explicitly intercepts
// the "System.Web" simple name and loads our app-local copy from disk. For every other name it
// returns null, which delegates to the Default context so the BCL resolves normally.
//
// NOTE: AssemblyLoadContext.Default.Resolving does NOT help here -- it only fires when default
// resolution FAILS, but the facade resolves successfully, so it would never fire. The custom ALC
// must therefore be in the resolution path of all System.Web-touching code: we load the host's own
// assembly INTO this ALC, then invoke the real entry point by reflection. From that point on,
// every type that code references (including System.Web.Server.HttpListenerServer and the worker
// request) is resolved through this ALC, which routes "System.Web" to our app-local DLL.
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace SampleHost
{
    internal sealed class SystemWebLoadContext : AssemblyLoadContext
    {
        private readonly string _systemWebPath;

        public SystemWebLoadContext(string systemWebPath)
            : base("SystemWebHostContext", isCollectible: false)
        {
            _systemWebPath = systemWebPath;
        }

        // Returning null falls back to the Default ALC (which would resolve the TPA facade), so we
        // MUST explicitly intercept "System.Web" and load our on-disk copy. Everything else -> null.
        protected override Assembly Load(AssemblyName assemblyName)
        {
            if (assemblyName != null && assemblyName.Name == "System.Web")
            {
                return LoadFromAssemblyPath(_systemWebPath);
            }
            return null;
        }
    }

    internal static class AlcBootstrap
    {
        // Real Main (Default ALC). Builds the custom ALC, loads the host assembly into it, and
        // invokes RealEntry.Run via reflection so the entire serving path executes inside the ALC.
        public static int Bootstrap(string[] args)
        {
            string baseDir = AppContext.BaseDirectory;
            string systemWebPath = Path.Combine(baseDir, "System.Web.dll");
            if (!File.Exists(systemWebPath))
            {
                Console.Error.WriteLine("Could not find app-local System.Web.dll at: " + systemWebPath);
                return 2;
            }

            SystemWebLoadContext alc = new SystemWebLoadContext(systemWebPath);

            // Load THIS host's own assembly (SampleHost) into the custom ALC. The copy loaded here
            // is a distinct instance from the Default-context one running Main, and any System.Web
            // types it touches resolve through the custom ALC.
            string hostPath = Path.Combine(baseDir, "SampleHost.dll");
            Assembly hostInAlc = alc.LoadFromAssemblyPath(hostPath);

            Type realEntry = hostInAlc.GetType("SampleHost.RealEntry", throwOnError: true);
            MethodInfo run = realEntry.GetMethod(
                "Run",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new Type[] { typeof(string[]) },
                null);
            if (run == null)
            {
                Console.Error.WriteLine("Could not resolve SampleHost.RealEntry.Run(string[]).");
                return 3;
            }

            object result = run.Invoke(null, new object[] { args });
            if (result is int)
            {
                return (int)result;
            }
            return 0;
        }
    }
}
