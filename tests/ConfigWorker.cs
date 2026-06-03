using System;
using System.Configuration;
using System.Web.Configuration;

namespace System.Web.Tests
{
    // Worker executed INSIDE the custom AssemblyLoadContext (via
    // SystemWebUnderTest.RunInAlc). Because this code runs in the ALC,
    // references to System.Web.* types here bind to OUR clean-room assembly,
    // and System.Configuration section type resolution that happens while this
    // method is on the stack resolves "..., System.Web" to ours as well.
    //
    // Returns a flat object[] of parsed values so the xUnit test (running in the
    // default ALC) can assert on primitives/strings without sharing types across
    // the ALC boundary.
    public static class ConfigWorker
    {
        // Opens the web.config at configFilePath via WebConfigurationManager and
        // returns parsed values in a fixed order:
        //   [0]  HttpRuntimeSection.MaxRequestLength      (int)
        //   [1]  HttpRuntimeSection.ExecutionTimeout      (TimeSpan)
        //   [2]  CompilationSection.Debug                 (bool)
        //   [3]  PagesSection.EnableViewState             (bool)
        //   [4]  PagesSection.EnableSessionState.ToString() (string; "True"/"False"/"ReadOnly")
        //   [5]  PagesSection.MasterPageFile              (string)
        //   [6]  AuthenticationSection.Mode.ToString()    (string)
        //   [7]  AuthenticationSection.Forms.LoginUrl     (string)
        //   [8]  SessionStateSection.Mode.ToString()      (string)
        //   [9]  SessionStateSection.Timeout              (TimeSpan)
        public static object[] ReadAll(string configFilePath)
        {
            string dir = System.IO.Path.GetDirectoryName(configFilePath);
            WebConfigurationFileMap map = new WebConfigurationFileMap();
            map.VirtualDirectories.Add("/", new VirtualDirectoryMapping(dir, true));

            System.Configuration.Configuration config =
                WebConfigurationManager.OpenMappedWebConfiguration(map, "/");

            // Register the section instances programmatically (with OUR section types,
            // bound in code through this ALC) so System.Configuration deserializes them
            // straight from the file's top-level XML WITHOUT resolving a
            // "..., System.Web" type string -- which would bind to the shared-framework
            // facade in the Default load context and fail with TypeLoadException.
            HttpRuntimeSection httpRuntime =
                (HttpRuntimeSection)GetOrAddSection(config, "httpRuntime", new HttpRuntimeSection());
            CompilationSection compilation =
                (CompilationSection)GetOrAddSection(config, "compilation", new CompilationSection());
            PagesSection pages =
                (PagesSection)GetOrAddSection(config, "pages", new PagesSection());
            AuthenticationSection authentication =
                (AuthenticationSection)GetOrAddSection(config, "authentication", new AuthenticationSection());
            SessionStateSection sessionState =
                (SessionStateSection)GetOrAddSection(config, "sessionState", new SessionStateSection());

            return new object[]
            {
                httpRuntime.MaxRequestLength,
                httpRuntime.ExecutionTimeout,
                compilation.Debug,
                pages.EnableViewState,
                pages.EnableSessionState.ToString(),
                pages.MasterPageFile,
                authentication.Mode.ToString(),
                authentication.Forms.LoginUrl,
                sessionState.Mode.ToString(),
                sessionState.Timeout,
            };
        }

        // Registers a fresh instance of OUR section type under the given top-level
        // section name (if not already present), then returns the deserialized section.
        // Registering the instance in code (its type bound through this ALC) means
        // System.Configuration deserializes from the file's raw XML without resolving a
        // "..., System.Web" type string against the facade.
        private static System.Configuration.ConfigurationSection GetOrAddSection(
            System.Configuration.Configuration config, string sectionName,
            System.Configuration.ConfigurationSection prototype)
        {
            // A section that appears in the file without a <configSections> declaration
            // is bound by System.Configuration to a generic DefaultSection. We grab that
            // section's raw XML and feed it through OUR section type's own
            // DeserializeSection(XmlReader) -- exercising our real parsing/property code
            // -- so no "..., System.Web" type string is ever resolved against the facade.
            System.Configuration.ConfigurationSection raw = config.GetSection(sectionName);
            string xml = raw.SectionInformation.GetRawXml();
            if (string.IsNullOrEmpty(xml))
            {
                return prototype; // section absent: caller sees defaults
            }

            System.Reflection.MethodInfo deserialize = typeof(System.Configuration.ConfigurationSection)
                .GetMethod("DeserializeSection",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public,
                    null,
                    new Type[] { typeof(System.Xml.XmlReader) },
                    null);
            if (deserialize == null)
            {
                throw new MissingMethodException(
                    "ConfigurationSection.DeserializeSection(XmlReader) not found.");
            }
            using (System.IO.StringReader sr = new System.IO.StringReader(xml))
            using (System.Xml.XmlReader reader = System.Xml.XmlReader.Create(sr))
            {
                deserialize.Invoke(prototype, new object[] { reader });
            }
            return prototype;
        }
    }
}