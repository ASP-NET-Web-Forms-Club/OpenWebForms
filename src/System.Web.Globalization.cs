// clean-room System.Web.Globalization implementation.
#nullable disable
#pragma warning disable

namespace System.Web.Globalization
{
    public static class StringLocalizerProviders
    {
        private static global::System.Web.Globalization.IStringLocalizerProvider _dataAnnotationProvider =
            new global::System.Web.Globalization.ResourceFileStringLocalizerProvider();

        public static global::System.Web.Globalization.IStringLocalizerProvider DataAnnotationStringLocalizerProvider
        {
            get { return _dataAnnotationProvider; }
            set { _dataAnnotationProvider = value; }
        }
    }
    public interface IStringLocalizerProvider
    {
        global::System.String GetLocalizedString(global::System.Globalization.CultureInfo culture, global::System.String name, params global::System.Object[] arguments);
    }
    public sealed class ResourceFileStringLocalizerProvider : global::System.Web.Globalization.IStringLocalizerProvider
    {
        // Caches one ResourceManager per resolved resource base name so repeated lookups do
        // not re-scan loaded assemblies.
        private readonly global::System.Collections.Generic.Dictionary<global::System.String, global::System.Resources.ResourceManager> _managers =
            new global::System.Collections.Generic.Dictionary<global::System.String, global::System.Resources.ResourceManager>(global::System.StringComparer.Ordinal);
        private readonly global::System.Object _sync = new global::System.Object();

        public ResourceFileStringLocalizerProvider() { }

        public global::System.String GetLocalizedString(global::System.Globalization.CultureInfo culture, global::System.String name, params global::System.Object[] arguments)
        {
            if (name == null) { throw new global::System.ArgumentNullException("name"); }
            if (culture == null) { culture = global::System.Globalization.CultureInfo.CurrentUICulture; }

            global::System.String text = null;
            global::System.Resources.ResourceManager rm = GetResourceManager();
            if (rm != null)
            {
                try { text = rm.GetString(name, culture); }
                catch (global::System.Resources.MissingManifestResourceException) { text = null; }
                catch (global::System.InvalidOperationException) { text = null; }
            }

            // Fall back to the raw name when no resource entry exists; this mirrors the
            // documented behavior of returning a usable string rather than null.
            if (text == null) { text = name; }

            if (arguments != null && arguments.Length > 0)
            {
                try { text = global::System.String.Format(culture, text, arguments); }
                catch (global::System.FormatException) { /* leave text unformatted on malformed placeholders */ }
            }
            return text;
        }

        // Resolves the "DataAnnotation.Localization" resource base name against the
        // currently loaded assemblies. The resource is supplied by the host application;
        // when absent this returns null and GetLocalizedString degrades to the key.
        private global::System.Resources.ResourceManager GetResourceManager()
        {
            lock (_sync)
            {
                global::System.Resources.ResourceManager cached;
                if (_managers.TryGetValue(ResourceFileName, out cached)) { return cached; }

                global::System.Resources.ResourceManager result = null;
                global::System.Reflection.Assembly[] assemblies = global::System.AppDomain.CurrentDomain.GetAssemblies();
                for (global::System.Int32 i = 0; i < assemblies.Length && result == null; i++)
                {
                    global::System.Reflection.Assembly asm = assemblies[i];
                    if (asm.IsDynamic) { continue; }
                    global::System.String[] resNames;
                    try { resNames = asm.GetManifestResourceNames(); }
                    catch { continue; }
                    for (global::System.Int32 j = 0; j < resNames.Length; j++)
                    {
                        global::System.String rn = resNames[j];
                        if (rn != null &&
                            (rn.Equals(ResourceFileName + ".resources", global::System.StringComparison.Ordinal) ||
                             rn.EndsWith("." + ResourceFileName + ".resources", global::System.StringComparison.Ordinal)))
                        {
                            try { result = new global::System.Resources.ResourceManager(ResourceFileName, asm); }
                            catch { result = null; }
                            break;
                        }
                    }
                }

                _managers[ResourceFileName] = result;
                return result;
            }
        }

        public const global::System.String ResourceFileName = "DataAnnotation.Localization";
    }
}
