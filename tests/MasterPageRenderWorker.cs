using System;
using System.IO;
using System.Reflection;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace System.Web.Tests
{
    // Worker executed INSIDE the custom AssemblyLoadContext (via RunInAlc). Drives a full
    // content-page + master-page MERGE through the page lifecycle against OUR clean-room System.Web:
    //
    //   * the content page registers its <asp:Content> templates (Page.AddContentTemplate),
    //   * a master with chrome (header/footer literals) around a ContentPlaceHolder is injected,
    //   * Page.ApplyMasterPage copies the content templates onto the master and makes the master the
    //     page's single child,
    //   * InitRecursive fires the ContentPlaceHolder's OnInit, which instantiates the matching content
    //     into the placeholder (replacing its default content),
    //   * rendering the page yields the master chrome wrapped around the page's content.
    internal static class MasterPageRenderWorker
    {
        private static readonly BindingFlags Inst =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private sealed class BodyTemplate : ITemplate
        {
            private readonly string _text;
            public BodyTemplate(string text) { _text = text; }
            public void InstantiateIn(Control container)
            {
                container.Controls.Add(new LiteralControl(_text));
            }
        }

        // A content page that exposes the protected-internal merge hooks to the worker and lets the
        // master be injected directly (no BuildManager / .aspx compilation needed for the test).
        private sealed class ContentPage : Page
        {
            public void Register(string placeholderId, string text)
            {
                AddContentTemplate(placeholderId, new BodyTemplate(text));
            }

            public void InjectMaster(MasterPage master)
            {
                // Set _masterPageFile so ApplyMasterPage runs, and _master so Master returns ours
                // instead of going through ResolveMaster/BuildManager.
                SetField(typeof(Page), this, "_masterPageFile", "~/Site.master");
                SetField(typeof(Page), this, "_master", master);
            }

            public void Merge() { ApplyMasterPage(); }
        }

        private static void SetField(Type declaring, object target, string name, object value)
        {
            FieldInfo fi = declaring.GetField(name, Inst);
            if (fi == null) { throw new MissingFieldException(declaring.FullName + "." + name); }
            fi.SetValue(target, value);
        }

        private static void InitRecursive(Control c)
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

        // Returns object[]:
        //   [0] html (string)                 -- full rendered page markup
        //   [1] contains "MASTER-HEADER"      (bool) -- master chrome present
        //   [2] contains "MASTER-FOOTER"      (bool)
        //   [3] contains "PAGE-CONTENT"       (bool) -- the content page's content merged in
        //   [4] NOT contains "DEFAULT-INSIDE" (bool) -- placeholder default content replaced
        //   [5] ordered header<content<footer (bool)
        //   [6] master is page's single child (bool)
        public static object[] ContentPageMergesWithMaster()
        {
            // ---- build the master with chrome around a ContentPlaceHolder ("Main") ----
            MasterPage master = new MasterPage();
            master.Controls.Add(new LiteralControl("MASTER-HEADER"));

            ContentPlaceHolder ph = new ContentPlaceHolder();
            ph.ID = "Main";
            ph.Controls.Add(new LiteralControl("DEFAULT-INSIDE")); // default content, should be replaced
            master.Controls.Add(ph);

            master.Controls.Add(new LiteralControl("MASTER-FOOTER"));

            // ---- the content page registers its <asp:Content> for "Main" and merges ----
            ContentPage page = new ContentPage();
            page.Register("Main", "PAGE-CONTENT");
            page.InjectMaster(master);
            page.Merge();   // ApplyMasterPage: copies templates onto master, master becomes sole child

            bool masterIsSoleChild = page.Controls.Count == 1 && ReferenceEquals(page.Controls[0], master);

            // ---- run init (fires ContentPlaceHolder.OnInit -> instantiates content) and render ----
            InitRecursive(page);
            string html = Render(page);

            int h = html.IndexOf("MASTER-HEADER", StringComparison.Ordinal);
            int c = html.IndexOf("PAGE-CONTENT", StringComparison.Ordinal);
            int f = html.IndexOf("MASTER-FOOTER", StringComparison.Ordinal);

            return new object[]
            {
                html,
                html.Contains("MASTER-HEADER"),
                html.Contains("MASTER-FOOTER"),
                html.Contains("PAGE-CONTENT"),
                !html.Contains("DEFAULT-INSIDE"),
                h >= 0 && c > h && f > c,
                masterIsSoleChild,
            };
        }
    }
}
