using System;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace System.Web.Tests
{
    // Workers executed INSIDE the custom AssemblyLoadContext (via RunInAlc) so the SiteMap
    // infrastructure (SiteMap / XmlSiteMapProvider / SiteMapNode) and the SiteMapPath control all
    // bind to OUR clean-room System.Web rather than the shared-framework facade.
    //
    // Each worker writes a small Web.sitemap to a unique temp file (Path.GetTempPath -> cross
    // platform, deterministic) and points an XmlSiteMapProvider at its absolute path. Because the
    // path is rooted, the provider resolves it directly without depending on HttpRuntime app paths.
    internal static class SiteMapWorker
    {
        private static readonly BindingFlags Inst =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private const string SiteMapXml =
            "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\n" +
            "<siteMap xmlns=\"http://schemas.microsoft.com/AspNet/SiteMap-File-1.0\">\n" +
            "  <siteMapNode url=\"/Default.aspx\" title=\"Home\" description=\"Home page\">\n" +
            "    <siteMapNode url=\"/Products.aspx\" title=\"Products\" description=\"Catalog\">\n" +
            "      <siteMapNode url=\"/Products/Details.aspx\" title=\"Details\" description=\"Detail\" />\n" +
            "    </siteMapNode>\n" +
            "    <siteMapNode url=\"/About.aspx\" title=\"About\" description=\"About us\" />\n" +
            "  </siteMapNode>\n" +
            "</siteMap>\n";

        private static string WriteTempSiteMap()
        {
            string path = Path.Combine(Path.GetTempPath(),
                "sysweb_sitemap_" + Guid.NewGuid().ToString("N") + ".sitemap");
            File.WriteAllText(path, SiteMapXml);
            return path;
        }

        private static XmlSiteMapProvider BuildProvider(string filePath)
        {
            XmlSiteMapProvider provider = new XmlSiteMapProvider();
            NameValueCollection attrs = new NameValueCollection();
            attrs["siteMapFile"] = filePath; // absolute -> resolved directly
            provider.Initialize("TestXmlSiteMapProvider", attrs);
            return provider;
        }

        // Loads the temp Web.sitemap and inspects RootNode + child nodes.
        // Returns object[]:
        //   [0] root != null            (bool)
        //   [1] root.Title              (string) -- "Home"
        //   [2] root.Url                (string) -- "/Default.aspx"
        //   [3] root child count        (int)    -- 2 (Products, About)
        //   [4] first child Title       (string) -- "Products"
        //   [5] first child Url         (string) -- "/Products.aspx"
        //   [6] grandchild Title        (string) -- "Details"
        public static object[] LoadRootAndChildren()
        {
            string file = WriteTempSiteMap();
            try
            {
                XmlSiteMapProvider provider = BuildProvider(file);
                SiteMapNode root = provider.RootNode;

                string rootTitle = null, rootUrl = null;
                int childCount = 0;
                string childTitle = null, childUrl = null, grandTitle = null;
                if (root != null)
                {
                    rootTitle = root.Title;
                    rootUrl = root.Url;
                    SiteMapNodeCollection children = root.ChildNodes;
                    childCount = children == null ? 0 : children.Count;
                    if (childCount > 0)
                    {
                        SiteMapNode first = children[0];
                        childTitle = first.Title;
                        childUrl = first.Url;
                        SiteMapNodeCollection grand = first.ChildNodes;
                        if (grand != null && grand.Count > 0) { grandTitle = grand[0].Title; }
                    }
                }

                return new object[]
                {
                    root != null,
                    rootTitle,
                    rootUrl,
                    childCount,
                    childTitle,
                    childUrl,
                    grandTitle,
                };
            }
            finally
            {
                try { File.Delete(file); } catch { }
            }
        }

        // CurrentNode resolves for a request path matching a node URL.
        // Returns object[]:
        //   [0] current != null         (bool)
        //   [1] current.Title           (string) -- "Products"
        //   [2] current.Url             (string) -- "/Products.aspx"
        //   [3] parent.Title            (string) -- "Home"
        //   [4] FindSiteMapNode("/About.aspx").Title (string) -- "About"
        public static object[] CurrentNodeResolvesForPath()
        {
            string file = WriteTempSiteMap();
            HttpContext prior = HttpContext.Current;
            try
            {
                XmlSiteMapProvider provider = BuildProvider(file);

                // Drive SiteMap.CurrentNode through HttpContext.Current.Request.Path.
                FakeWorkerRequest wr = new FakeWorkerRequest("GET", "/Products.aspx", "", null, Array.Empty<byte>());
                HttpContext ctx = new HttpContext(wr);
                HttpContext.Current = ctx;

                SiteMapNode current = provider.CurrentNode;
                string title = null, url = null, parentTitle = null;
                if (current != null)
                {
                    title = current.Title;
                    url = current.Url;
                    SiteMapNode parent = current.ParentNode;
                    if (parent != null) { parentTitle = parent.Title; }
                }

                SiteMapNode about = provider.FindSiteMapNode("/About.aspx");

                return new object[]
                {
                    current != null,
                    title,
                    url,
                    parentTitle,
                    about == null ? null : about.Title,
                };
            }
            finally
            {
                HttpContext.Current = prior;
                try { File.Delete(file); } catch { }
            }
        }

        // A SiteMapPath bound to the provider renders the breadcrumb trail for the current node.
        // Current path is "/Products/Details.aspx" -> trail Home > Products > Details.
        // Returns object[]:
        //   [0] html (string) -- full rendered markup
        //   [1] contains "Home"     (bool)
        //   [2] contains "Products" (bool)
        //   [3] contains "Details"  (bool)
        //   [4] separator count     (int)  -- two " &gt; " separators between three nodes
        //   [5] Home before Products before Details (ordered) (bool)
        public static object[] SiteMapPathRendersBreadcrumb()
        {
            string file = WriteTempSiteMap();
            HttpContext prior = HttpContext.Current;
            try
            {
                XmlSiteMapProvider provider = BuildProvider(file);

                FakeWorkerRequest wr = new FakeWorkerRequest("GET", "/Products/Details.aspx", "", null, Array.Empty<byte>());
                HttpContext ctx = new HttpContext(wr);
                HttpContext.Current = ctx;

                SiteMapPath path = new SiteMapPath();
                path.Provider = provider; // bind directly to our temp provider

                MethodInfo init = typeof(Control).GetMethod("InitRecursive", Inst);
                init.Invoke(path, new object[] { null });

                StringWriter sw = new StringWriter();
                HtmlTextWriter w = new HtmlTextWriter(sw);
                path.RenderControl(w);
                w.Flush();
                string html = sw.ToString();

                int homeIdx = html.IndexOf("Home", StringComparison.Ordinal);
                int prodIdx = html.IndexOf("Products", StringComparison.Ordinal);
                int detIdx = html.IndexOf("Details", StringComparison.Ordinal);

                // The default PathSeparator is " > " rendered via a raw LiteralControl, so the
                // separator appears literally (not HTML-encoded) between the breadcrumb nodes.
                int sepCount = CountOccurrences(html, " > ");

                bool ordered = homeIdx >= 0 && prodIdx > homeIdx && detIdx > prodIdx;

                return new object[]
                {
                    html,
                    html.Contains("Home"),
                    html.Contains("Products"),
                    html.Contains("Details"),
                    sepCount,
                    ordered,
                };
            }
            finally
            {
                HttpContext.Current = prior;
                try { File.Delete(file); } catch { }
            }
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle)) { return 0; }
            int count = 0;
            int idx = 0;
            while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += needle.Length;
            }
            return count;
        }
    }
}
