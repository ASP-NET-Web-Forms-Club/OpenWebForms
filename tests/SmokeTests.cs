using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Xunit;

namespace System.Web.Tests
{
    // Tier 0 smoke tests.
    //
    // These assert the *identity* and *loadability* of our clean-room
    // reimplementation assembly, and that its pivotal frozen public types are
    // present. They deliberately do NOT invoke any method body -- at Tier 0
    // every body still throws NotImplementedException.
    //
    // Why a dedicated AssemblyLoadContext instead of plain typeof(...)?
    // net8.0's shared framework ships its own strong-named facade assembly also
    // named "System.Web" (PublicKeyToken=b03f5f7f11d50a3a) that only
    // type-forwards HttpUtility. That framework copy sits on the runtime's
    // trusted-platform-assemblies list and shadows our app-local
    // "System.Web.dll" (PublicKeyToken=null) in the default load context, so a
    // bare typeof(System.Web.HttpContext) would bind to the facade (which lacks
    // the type) and fail. To verify OUR assembly unambiguously we load it from
    // its on-disk path -- which the build copies next to the test assembly --
    // into an isolated load context and assert against that instance.
    public class SmokeTests
    {
        private static readonly Lazy<Assembly> OurSystemWeb = new Lazy<Assembly>(LoadOurSystemWeb);

        private static Assembly LoadOurSystemWeb()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "System.Web.dll");
            Assert.True(File.Exists(path), "Expected our System.Web.dll next to the test assembly at: " + path);
            var alc = new AssemblyLoadContext("SystemWebUnderTest", isCollectible: false);
            return alc.LoadFromAssemblyPath(path);
        }

        [Fact]
        public void OurSystemWebAssembly_LoadsFromAppBase()
        {
            Assembly a = OurSystemWeb.Value;
            Assert.NotNull(a);
        }

        [Fact]
        public void AssemblySimpleName_IsSystemWeb()
        {
            Assert.Equal("System.Web", OurSystemWeb.Value.GetName().Name);
        }

        [Fact]
        public void AssemblyVersion_Is4000()
        {
            Assert.Equal(new Version(4, 0, 0, 0), OurSystemWeb.Value.GetName().Version);
        }

        [Fact]
        public void Assembly_IsNotTheStrongNamedFrameworkFacade()
        {
            // Our reimplementation is unsigned (no public key token); the
            // framework facade is strong-named. This guards against accidentally
            // testing the framework copy.
            byte[] token = OurSystemWeb.Value.GetName().GetPublicKeyToken();
            Assert.True(token == null || token.Length == 0,
                "Loaded assembly is strong-named -- this is the framework facade, not our reimplementation.");
        }

        [Theory]
        [InlineData("System.Web.HttpRuntime")]
        [InlineData("System.Web.HttpContext")]
        [InlineData("System.Web.HttpApplication")]
        [InlineData("System.Web.HttpWorkerRequest")]
        [InlineData("System.Web.UI.Page")]
        [InlineData("System.Web.UI.Control")]
        public void PivotalType_IsPresentInOurAssembly(string fullName)
        {
            Type t = OurSystemWeb.Value.GetType(fullName, throwOnError: false);
            Assert.NotNull(t);
        }

        [Fact]
        public void HttpWorkerRequest_IsAbstract()
        {
            // A cheap structural assertion that does not run any method body:
            // the request-spine base type must remain abstract.
            Type t = OurSystemWeb.Value.GetType("System.Web.HttpWorkerRequest", throwOnError: false);
            Assert.NotNull(t);
            Assert.True(t.IsAbstract, "HttpWorkerRequest should be abstract.");
        }

        [Fact]
        public void Control_IsAssignableBase_OfPage()
        {
            // Page : ... : Control. Structural metadata only; no body executes.
            Type control = OurSystemWeb.Value.GetType("System.Web.UI.Control", throwOnError: false);
            Type page = OurSystemWeb.Value.GetType("System.Web.UI.Page", throwOnError: false);
            Assert.NotNull(control);
            Assert.NotNull(page);
            Assert.True(control.IsAssignableFrom(page), "Page should derive from Control.");
        }
    }
}
