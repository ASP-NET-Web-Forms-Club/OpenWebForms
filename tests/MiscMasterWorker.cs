using System;
using System.IO;
using System.Reflection;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace System.Web.Tests
{
    // Workers executed INSIDE the custom AssemblyLoadContext (via RunInAlc) so Calendar / MasterPage /
    // ContentPlaceHolder / Content / XmlDataSource bind to OUR clean-room System.Web rather than the
    // shared-framework facade.
    //
    // Covers:
    //   * Calendar renders a month <table> for a fixed VisibleDate, with day cells present.
    //   * MasterPage + ContentPlaceHolder + Content merge programmatically: a Content region's
    //     template is registered with the master and instantiated into the matching placeholder,
    //     replacing the placeholder's default content.
    //   * XmlDataSource over an inline XML string yields a hierarchy that feeds a TreeView.
    internal static class MiscMasterWorker
    {
        private static readonly BindingFlags Inst =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static void Init(Control c)
        {
            MethodInfo mi = typeof(Control).GetMethod("InitRecursive", Inst);
            mi.Invoke(c, new object[] { null });
        }

        private static string Render(Control c)
        {
            StringWriter sw = new StringWriter();
            HtmlTextWriter w = new HtmlTextWriter(sw);
            c.RenderControl(w);
            w.Flush();
            return sw.ToString();
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

        // ===================== Calendar =====================

        // Calendar with a fixed VisibleDate (June 2024) and no Selection/Page. Renders a month table.
        // Returns object[]:
        //   [0] html starts "<table"
        //   [1] number of <tr> (>= 6 week rows; title + header add more)  -> >= 6
        //   [2] number of <td>/<th> day-ish cells is large (>= 42 day cells)
        //   [3] contains "15"  (the 15th of the visible month)
        //   [4] contains "June" (month title)
        //   [5] contains "30"  (June has 30 days)
        public static object[] CalendarMonth()
        {
            Calendar cal = new Calendar();
            cal.VisibleDate = new DateTime(2024, 6, 15);
            cal.TodaysDate = new DateTime(2024, 6, 15);

            Init(cal);
            string html = Render(cal);
            string lower = html.ToLowerInvariant();

            int trCount = CountOccurrences(lower, "<tr");
            int tdCount = CountOccurrences(lower, "<td");

            return new object[]
            {
                html.StartsWith("<table"),
                trCount,
                tdCount,
                html.Contains(">15<"),
                html.Contains("June"),
                html.Contains(">30<"),
            };
        }

        // ===================== MasterPage + ContentPlaceHolder + Content =====================

        private sealed class BodyTemplate : ITemplate
        {
            private readonly string _text;
            public BodyTemplate(string text) { _text = text; }
            public void InstantiateIn(Control container)
            {
                container.Controls.Add(new LiteralControl(_text));
            }
        }

        // Builds a master with header/footer literals around a ContentPlaceHolder (default content),
        // registers a Content template for that placeholder, inits the tree (which triggers the
        // placeholder to instantiate the content), and renders.
        // Returns object[]:
        //   [0] contains "MASTER-HEADER"
        //   [1] contains "MASTER-FOOTER"
        //   [2] contains "CONTENT-BODY"  (the supplied content rendered into the placeholder)
        //   [3] does NOT contain "DEFAULT-PLACEHOLDER" (default content was replaced)
        //   [4] CONTENT-BODY rendered between header and footer
        //   [5] ContentPlaceHolder child count == 1 (the single content literal)
        public static object[] MasterContentMerge()
        {
            MasterPage master = new MasterPage();
            master.Controls.Add(new LiteralControl("MASTER-HEADER"));

            ContentPlaceHolder ph = new ContentPlaceHolder();
            ph.ID = "Main";
            ph.Controls.Add(new LiteralControl("DEFAULT-PLACEHOLDER"));
            master.Controls.Add(ph);

            master.Controls.Add(new LiteralControl("MASTER-FOOTER"));

            // Register the content template (protected internal -> reflection).
            MethodInfo add = typeof(MasterPage).GetMethod("AddContentTemplate", Inst);
            add.Invoke(master, new object[] { "Main", new BodyTemplate("CONTENT-BODY") });

            Init(master); // ContentPlaceHolder.OnInit instantiates the matching content template
            string html = Render(master);

            int headerIdx = html.IndexOf("MASTER-HEADER", StringComparison.Ordinal);
            int bodyIdx = html.IndexOf("CONTENT-BODY", StringComparison.Ordinal);
            int footerIdx = html.IndexOf("MASTER-FOOTER", StringComparison.Ordinal);

            return new object[]
            {
                html.Contains("MASTER-HEADER"),
                html.Contains("MASTER-FOOTER"),
                html.Contains("CONTENT-BODY"),
                !html.Contains("DEFAULT-PLACEHOLDER"),
                headerIdx >= 0 && bodyIdx > headerIdx && footerIdx > bodyIdx,
                ph.Controls.Count,
            };
        }

        // ===================== XmlDataSource -> TreeView =====================

        private const string Xml =
            "<Root>" +
              "<Folder name='Docs'>" +
                "<File name='a.txt' />" +
                "<File name='b.txt' />" +
              "</Folder>" +
              "<Folder name='Pics' />" +
            "</Root>";

        // XmlDataSource over an inline XML string yields a hierarchy. The hierarchy is fed into a
        // TreeView (as an IHierarchicalEnumerable) and DataBind populates the node tree mirroring the
        // XML element structure.
        // Returns object[]:
        //   [0] top-level hierarchy node count    -> 2 (the two <Folder> under <Root>)
        //   [1] first node Type/name              -> "Folder"
        //   [2] first node HasChildren            -> true (Docs has 2 files)
        //   [3] TreeView root Nodes.Count         -> 2
        //   [4] first TreeView node child count   -> 2 (a.txt, b.txt)
        //   [5] total nodes (roots + their direct children) -> 2 + 2 + 0 = 4
        public static object[] XmlDataSourceTree()
        {
            XmlDataSource xds = new XmlDataSource();
            xds.Data = Xml;

            // Inspect the hierarchy directly.
            global::System.Web.UI.IHierarchicalDataSource ids = xds;
            global::System.Web.UI.HierarchicalDataSourceView view = ids.GetHierarchicalView(string.Empty);
            global::System.Web.UI.IHierarchicalEnumerable top = view.Select();

            int topCount = 0;
            string firstType = null;
            bool firstHasChildren = false;
            foreach (object item in top)
            {
                global::System.Web.UI.IHierarchyData hd = top.GetHierarchyData(item);
                if (topCount == 0) { firstType = hd.Type; firstHasChildren = hd.HasChildren; }
                topCount++;
            }

            // Feed the hierarchy into a TreeView and bind.
            TreeView tree = new TreeView();
            tree.ExpandDepth = 10;
            tree.DataSource = ids.GetHierarchicalView(string.Empty).Select();
            Init(tree);
            tree.DataBind();

            int rootCount = tree.Nodes.Count;
            int firstChildCount = rootCount > 0 ? tree.Nodes[0].ChildNodes.Count : -1;
            int totalNodes = rootCount;
            for (int i = 0; i < tree.Nodes.Count; i++) { totalNodes += tree.Nodes[i].ChildNodes.Count; }

            return new object[]
            {
                topCount,
                firstType,
                firstHasChildren,
                rootCount,
                firstChildCount,
                totalNodes,
            };
        }
    }
}
