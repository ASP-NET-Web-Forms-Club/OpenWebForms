using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;

namespace System.Web.Tests
{
    // Workers executed INSIDE the custom AssemblyLoadContext (via RunInAlc) so the WebParts
    // controls bind to OUR clean-room System.Web rather than the shared-framework facade.
    //
    // The WebParts subsystem (Tier 5c) is exercised here against a hand-built control tree:
    //   * a WebPartManager + a WebPartZone whose ZoneTemplate yields a couple of WebParts
    //     (one real WebPart subclass, one Label wrapped via GenericWebPart);
    //   * the manager discovers the parts and the zone renders them with title-bar chrome;
    //   * a DisplayMode switch flips a ToolZone (CatalogZone) from hidden to visible;
    //   * a personalizable property round-trips through the default provider;
    //   * a provider/consumer WebPartConnection activates and the consumer receives data.
    //
    // The lifecycle is driven directly (InitRecursive / PreRenderRecursiveInternal /
    // RenderControl) rather than through a full HTTP request, so the assertions are
    // deterministic and do not depend on viewstate transport.
    internal static class WebPartsWorker
    {
        private static readonly BindingFlags Inst =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static void InitRecursive(Control c)
        {
            MethodInfo mi = typeof(Control).GetMethod("InitRecursive", Inst);
            mi.Invoke(c, new object[] { null });
        }

        private static void PreRenderRecursive(Control c)
        {
            MethodInfo mi = typeof(Control).GetMethod("PreRenderRecursiveInternal", Inst);
            if (mi != null) { mi.Invoke(c, null); }
        }

        private static string Render(Control c)
        {
            StringWriter sw = new StringWriter();
            HtmlTextWriter w = new HtmlTextWriter(sw);
            c.RenderControl(w);
            w.Flush();
            return sw.ToString();
        }

        // ---- A: discover + render with chrome ----
        public static object[] DiscoverAndRender()
        {
            WebPartTestPage page = new WebPartTestPage();
            InitRecursive(page);
            PreRenderRecursive(page);

            int managerCount = page.Manager.WebParts.Count;
            int zoneCount = page.Zone.WebParts.Count;

            string html = Render(page.Zone);

            bool currentSame = ReferenceEquals(
                WebPartManager.GetCurrentWebPartManager(page), page.Manager);

            return new object[]
            {
                managerCount,
                zoneCount,
                html.Contains("My First Part"),
                html.Contains("My Label Part"),
                html.Contains("<table"),
                html.Contains("PartBody-A"),
                html.Contains("Hello-Label"),
                currentSame,
            };
        }

        // ---- B: DisplayMode switch flips a ToolZone visibility ----
        public static object[] DisplayModeSwitch()
        {
            WebPartTestPage page = new WebPartTestPage();
            InitRecursive(page);

            string browseModeName = page.Manager.DisplayMode.Name;
            bool catalogVisibleInBrowse = page.Catalog.Visible;
            bool wpZoneVisibleBrowse = page.Zone.Visible;

            bool changed = false;
            page.Manager.DisplayModeChanged += (s, e) => { changed = true; };

            page.Manager.DisplayMode = WebPartManager.CatalogDisplayMode;

            bool catalogVisibleInCatalog = page.Catalog.Visible;
            bool wpZoneVisibleCatalog = page.Zone.Visible;
            string newModeName = page.Manager.DisplayMode.Name;

            return new object[]
            {
                browseModeName,
                catalogVisibleInBrowse,
                catalogVisibleInCatalog,
                changed,
                newModeName,
                wpZoneVisibleBrowse && wpZoneVisibleCatalog,
            };
        }

        // ---- C: personalization round-trip through the default provider ----
        public static object[] PersonalizationRoundTrip()
        {
            WebPartTestPage src = new WebPartTestPage();
            InitRecursive(src);
            PersonalizableWebPart srcPart = src.PersonalizablePart;
            string original = "PERSISTED-VALUE-42";
            srcPart.Greeting = original;
            int srcManagerCount = src.Manager.WebParts.Count;

            WebPartPersonalization perso = src.Manager.Personalization;
            PersonalizationProvider provider = (PersonalizationProvider)Invoke(perso, "EnsureProvider");
            PersonalizationState state = provider.LoadPersonalizationState(src.Manager, false);
            SetField(perso, "_state", state);
            Invoke(perso, "ExtractPersonalizationState");
            provider.SavePersonalizationState(state);

            WebPartTestPage dst = new WebPartTestPage();
            InitRecursive(dst);
            PersonalizableWebPart dstPart = dst.PersonalizablePart;
            string freshDefault = dstPart.Greeting;

            WebPartPersonalization perso2 = dst.Manager.Personalization;
            PersonalizationProvider provider2 = (PersonalizationProvider)Invoke(perso2, "EnsureProvider");
            PersonalizationState state2 = provider2.LoadPersonalizationState(dst.Manager, false);
            SetField(perso2, "_state", state2);
            Invoke(perso2, "ApplyPersonalizationState");

            string applied = dstPart.Greeting;

            return new object[]
            {
                original,
                freshDefault,
                applied,
                applied == original && freshDefault != original,
                srcManagerCount,
            };
        }

        // ---- D: WebPartConnection activates and the consumer receives the provider data ----
        public static object[] ConnectionActivates()
        {
            WebPartConnTestPage page = new WebPartConnTestPage();
            InitRecursive(page);

            ZipProviderPart provider = page.Provider;
            ZipConsumerPart consumer = page.Consumer;
            WebPartManager mgr = page.Manager;

            ProviderConnectionPointCollection pp = mgr.GetProviderConnectionPoints(provider);
            ConsumerConnectionPointCollection cp = mgr.GetConsumerConnectionPoints(consumer);
            bool providerFound = pp != null && pp.Count > 0;
            bool consumerFound = cp != null && cp.Count > 0;

            ProviderConnectionPoint pcp = providerFound ? pp[0] : null;
            ConsumerConnectionPoint ccp = consumerFound ? cp[0] : null;

            WebPartConnection conn = null;
            if (pcp != null && ccp != null)
            {
                conn = mgr.ConnectWebParts(provider, pcp, consumer, ccp);
            }
            bool created = conn != null;

            PreRenderRecursive(page);
            if (conn != null) { Invoke(mgr, "ActivateConnection", conn); }

            bool active = conn != null && conn.IsActive;
            string received = consumer.ReceivedZip;
            string expected = provider.Zip;

            return new object[]
            {
                providerFound,
                consumerFound,
                created,
                active,
                received,
                expected,
            };
        }

        // ---- reflection helpers (members are internal/protected on our types) ----
        private static object Invoke(object target, string method, params object[] args)
        {
            Type t = target.GetType();
            MethodInfo mi = null;
            while (t != null && mi == null)
            {
                MethodInfo[] all = t.GetMethods(Inst);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].Name == method && all[i].GetParameters().Length == (args == null ? 0 : args.Length))
                    {
                        mi = all[i]; break;
                    }
                }
                t = t.BaseType;
            }
            if (mi == null) { throw new MissingMethodException(target.GetType().FullName + "." + method); }
            return mi.Invoke(target, args);
        }

        private static void SetField(object target, string field, object value)
        {
            Type t = target.GetType();
            FieldInfo fi = null;
            while (t != null && fi == null)
            {
                fi = t.GetField(field, Inst);
                t = t.BaseType;
            }
            if (fi == null) { throw new MissingFieldException(target.GetType().FullName + "." + field); }
            fi.SetValue(target, value);
        }
    }
}
