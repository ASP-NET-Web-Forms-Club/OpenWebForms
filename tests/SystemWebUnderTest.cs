using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace System.Web.Tests
{
    // Test bridge for binding to OUR clean-room System.Web instead of the
    // strong-named shared-framework facade.
    //
    // WHY (same problem the host's AlcBootstrap solves):
    // net8.0's shared framework ships a strong-named "System.Web" facade
    // (PublicKeyToken=b03f5f7f11d50a3a) on the trusted-platform-assemblies
    // list that only type-forwards HttpUtility. In the DEFAULT
    // AssemblyLoadContext a bare reference to System.Web binds to that facade,
    // so any test that touches our types (HttpEncoder, Cache, the config
    // sections) would bind to the facade and fail to find them. Our assembly
    // shares the SAME identity (4.0.0.0, unsigned), so a version bump cannot
    // disambiguate.
    //
    // FIX: load our app-local System.Web.dll into a dedicated
    // AssemblyLoadContext whose Load() intercepts the "System.Web" simple name
    // and returns our on-disk copy; for every other name it returns null so the
    // BCL resolves normally through the Default context. This mirrors
    // samples/host/AlcBootstrap.cs.
    //
    // Two ways to drive our code through the ALC:
    //   1. Reflection helpers (Type/CreateInstance/Invoke/InvokeStatic/GetProperty)
    //      -- enough for leaf utilities (HttpEncoder) and the Cache, whose
    //      behavior does not depend on resolving assembly-qualified type names.
    //   2. RunInAlc(workerTypeName, method, args) -- loads a worker type FROM
    //      THIS test assembly INTO the ALC and invokes it there. Code executing
    //      inside the ALC resolves "System.Web" through the ALC, which matters
    //      for System.Configuration section deserialization: it resolves each
    //      section's CLR type via Type.GetType against the calling code's load
    //      context, so the config worker must run inside the ALC for the
    //      assembly-qualified "..., System.Web" type names to bind to OURS.
    internal sealed class SystemWebUnderTest
    {
        private sealed class SystemWebLoadContext : AssemblyLoadContext
        {
            private readonly string _systemWebPath;

            public SystemWebLoadContext(string systemWebPath)
                : base("SystemWebUnderTest", isCollectible: false)
            {
                _systemWebPath = systemWebPath;
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                if (assemblyName != null && assemblyName.Name == "System.Web")
                {
                    return LoadFromAssemblyPath(_systemWebPath);
                }
                return null;
            }
        }

        private static readonly Lazy<SystemWebUnderTest> s_instance =
            new Lazy<SystemWebUnderTest>(() => new SystemWebUnderTest());

        public static SystemWebUnderTest Instance => s_instance.Value;

        private readonly SystemWebLoadContext _alc;
        private readonly Assembly _systemWeb;
        private readonly string _testAssemblyPath;

        private SystemWebUnderTest()
        {
            string baseDir = AppContext.BaseDirectory;
            string systemWebPath = Path.Combine(baseDir, "System.Web.dll");
            if (!File.Exists(systemWebPath))
            {
                throw new FileNotFoundException(
                    "Expected our System.Web.dll next to the test assembly at: " + systemWebPath);
            }
            _alc = new SystemWebLoadContext(systemWebPath);
            _systemWeb = _alc.LoadFromAssemblyPath(systemWebPath);
            _testAssemblyPath = typeof(SystemWebUnderTest).Assembly.Location;
        }

        public Assembly Assembly => _systemWeb;
        public AssemblyLoadContext LoadContext => _alc;

        // Resolve a type by full name from OUR System.Web (e.g. "System.Web.Caching.Cache").
        public Type Type(string fullName)
        {
            Type t = _systemWeb.GetType(fullName, throwOnError: false);
            if (t == null)
            {
                throw new TypeLoadException("Type not found in our System.Web: " + fullName);
            }
            return t;
        }

        // Resolve an enum value from OUR System.Web by enum type full name + member name.
        public object EnumValue(string enumFullName, string memberName)
        {
            Type t = Type(enumFullName);
            return Enum.Parse(t, memberName);
        }

        public object CreateInstance(string fullName, params object[] args)
        {
            return Activator.CreateInstance(Type(fullName), args);
        }

        private const BindingFlags AllInstance =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags AllStatic =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        // Invoke an instance method (public or non-public) by name with a best-effort
        // overload match on argument count and assignability.
        public object Invoke(object instance, string method, params object[] args)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            MethodInfo mi = FindMethod(instance.GetType(), method, AllInstance, args);
            return mi.Invoke(instance, args);
        }

        // Invoke a static method on a type resolved from our assembly.
        public object InvokeStatic(string typeFullName, string method, params object[] args)
        {
            Type t = Type(typeFullName);
            MethodInfo mi = FindMethod(t, method, AllStatic, args);
            return mi.Invoke(null, args);
        }

        public object GetProperty(object instance, string name)
        {
            PropertyInfo pi = instance.GetType().GetProperty(name, AllInstance);
            if (pi == null) throw new MissingMemberException(instance.GetType().FullName, name);
            return pi.GetValue(instance);
        }

        public object GetStaticProperty(string typeFullName, string name)
        {
            Type t = Type(typeFullName);
            PropertyInfo pi = t.GetProperty(name, AllStatic);
            if (pi == null) throw new MissingMemberException(typeFullName, name);
            return pi.GetValue(null);
        }

        public object GetStaticField(string typeFullName, string name)
        {
            Type t = Type(typeFullName);
            FieldInfo fi = t.GetField(name, AllStatic);
            if (fi == null) throw new MissingMemberException(typeFullName, name);
            return fi.GetValue(null);
        }

        private static MethodInfo FindMethod(Type t, string name, BindingFlags flags, object[] args)
        {
            int argc = args == null ? 0 : args.Length;
            MethodInfo best = null;
            MethodInfo[] all = t.GetMethods(flags);
            for (int i = 0; i < all.Length; i++)
            {
                MethodInfo m = all[i];
                if (m.Name != name) continue;
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length != argc) continue;
                bool ok = true;
                for (int j = 0; j < argc; j++)
                {
                    object a = args[j];
                    Type pt = ps[j].ParameterType;
                    if (pt.IsByRef) pt = pt.GetElementType();
                    if (a == null)
                    {
                        if (pt.IsValueType && Nullable.GetUnderlyingType(pt) == null) { ok = false; break; }
                    }
                    else if (!pt.IsInstanceOfType(a))
                    {
                        ok = false; break;
                    }
                }
                if (ok) { best = m; break; }
            }
            if (best == null)
            {
                // Fall back to a name-and-arity match so a null arg whose param is a
                // reference type still binds when IsInstanceOfType could not confirm.
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].Name == name && all[i].GetParameters().Length == argc)
                    {
                        best = all[i];
                        break;
                    }
                }
            }
            if (best == null)
            {
                throw new MissingMethodException(t.FullName + "." + name + " (arity " + argc + ")");
            }
            return best;
        }

        // Load a worker type (defined in THIS test assembly) INTO the ALC and call a
        // static method on it. The worker then executes with "System.Web" resolving to
        // OURS -- required for System.Configuration section type resolution.
        public object RunInAlc(string workerTypeFullName, string staticMethod, params object[] args)
        {
            Assembly testInAlc = _alc.LoadFromAssemblyPath(_testAssemblyPath);
            Type worker = testInAlc.GetType(workerTypeFullName, throwOnError: true);
            MethodInfo mi = worker.GetMethod(staticMethod, AllStatic);
            if (mi == null)
            {
                throw new MissingMethodException(workerTypeFullName + "." + staticMethod);
            }
            return mi.Invoke(null, args);
        }
    }
}
