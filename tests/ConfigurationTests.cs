using System;
using System.IO;
using Xunit;

namespace System.Web.Tests
{
    // Tier 1 behavioral tests for the System.Web.Configuration sections.
    //
    // We write a small web.config to a temp directory, then open and parse it
    // through OUR WebConfigurationManager. The actual open + GetSection + typed
    // reads run inside the ALC (ConfigWorker, via RunInAlc) so that:
    //   - WebConfigurationManager / the section classes bind to ours, and
    //   - System.Configuration's section-type resolution of the
    //     "..., System.Web" assembly-qualified type names binds to ours.
    //
    // The web.config declares <configSections> mapping each section name to the
    // corresponding clean-room section type in our assembly, mirroring how
    // machine.config registers them in the real framework.
    public class ConfigurationTests
    {
        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        // No <configSections> declarations: the ConfigWorker registers OUR section
        // types programmatically (in-code, bound through the ALC) before reading them,
        // so System.Configuration never resolves a "..., System.Web" type string against
        // the shared-framework facade in the Default load context.
        private const string ConfigXml =
@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <httpRuntime maxRequestLength=""8192"" executionTimeout=""00:03:20"" />
  <compilation debug=""true"" />
  <pages enableViewState=""false"" enableSessionState=""true"" masterPageFile=""~/Site.master"" />
  <authentication mode=""Forms"">
    <forms loginUrl=""~/Account/Login.aspx"" />
  </authentication>
  <sessionState mode=""InProc"" timeout=""00:40:00"" />
</configuration>";

        private static string WriteTempConfig()
        {
            string dir = Path.Combine(Path.GetTempPath(),
                "SystemWebTier1Cfg_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "web.config");
            File.WriteAllText(path, ConfigXml);
            return path;
        }

        private static object[] ParseConfig()
        {
            string path = WriteTempConfig();
            try
            {
                object result = Web.RunInAlc(
                    "System.Web.Tests.ConfigWorker", "ReadAll", path);
                return (object[])result;
            }
            finally
            {
                try { Directory.Delete(Path.GetDirectoryName(path), true); } catch { }
            }
        }

        [Fact]
        public void HttpRuntimeSection_MaxRequestLength_And_ExecutionTimeout()
        {
            object[] v = ParseConfig();
            Assert.Equal(8192, (int)v[0]);
            Assert.Equal(TimeSpan.FromSeconds(200), (TimeSpan)v[1]);
        }

        [Fact]
        public void CompilationSection_Debug_True()
        {
            object[] v = ParseConfig();
            Assert.True((bool)v[2]);
        }

        [Fact]
        public void PagesSection_ViewState_Session_MasterPage()
        {
            object[] v = ParseConfig();
            Assert.False((bool)v[3]);                    // enableViewState=false
            Assert.Equal("True", (string)v[4]);          // enableSessionState=true -> PagesEnableSessionState.True
            Assert.Equal("~/Site.master", (string)v[5]); // masterPageFile
        }

        [Fact]
        public void AuthenticationSection_Mode_Forms_And_LoginUrl()
        {
            object[] v = ParseConfig();
            Assert.Equal("Forms", (string)v[6]);
            Assert.Equal("~/Account/Login.aspx", (string)v[7]);
        }

        [Fact]
        public void SessionStateSection_Mode_And_Timeout()
        {
            object[] v = ParseConfig();
            Assert.Equal("InProc", (string)v[8]);
            Assert.Equal(TimeSpan.FromMinutes(40), (TimeSpan)v[9]);
        }
    }
}