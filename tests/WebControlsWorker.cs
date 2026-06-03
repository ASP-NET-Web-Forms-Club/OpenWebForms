using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;

namespace System.Web.Tests
{
    // Worker executed INSIDE the custom AssemblyLoadContext (via RunInAlc), so the WebControls
    // base types (WebControl/Label/TextBox/Button/DropDownList/CheckBox) bind to OUR clean-room
    // System.Web rather than the shared-framework facade.
    //
    // Two families of checks:
    //   * RENDER: build a small control tree, run it through Init so view state tracks, set
    //     properties, and render via HtmlTextWriter. Assert on the emitted markup.
    //   * POSTBACK: drive a full Page GET -> POST through ProcessRequest, replaying the scraped
    //     __VIEWSTATE plus posted form values, and observe IPostBackDataHandler load + change
    //     events on TextBox / CheckBox / DropDownList.
    internal static class WebControlsWorker
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

        // ---- Unit parse / ToString round-trips (pure value-type, no tree needed) ----
        // Returns object[]: [0] "100px".ToString, [1] "50%".ToString, [2] Pixel value,
        //                   [3] Percentage value, [4] empty unit ToString
        public static object[] UnitParse()
        {
            Unit px = Unit.Parse("100px");
            Unit pct = Unit.Parse("50%");
            Unit empty = Unit.Parse("");
            return new object[]
            {
                px.ToString(System.Globalization.CultureInfo.InvariantCulture),
                pct.ToString(System.Globalization.CultureInfo.InvariantCulture),
                px.Value,
                pct.Value,
                empty.IsEmpty,
                empty.ToString(System.Globalization.CultureInfo.InvariantCulture),
                px.Type.ToString(),
                pct.Type.ToString(),
            };
        }

        // ---- Render a Label / TextBox / Button / DropDownList tree ----
        // Returns object[]: [0] labelHtml, [1] textBoxHtml, [2] buttonHtml, [3] dropDownHtml
        public static object[] RenderControls()
        {
            // Label
            Label label = new Label();
            label.ID = "lbl";
            label.Text = "Hello";
            Init(label);
            string labelHtml = Render(label);

            // TextBox
            TextBox box = new TextBox();
            box.ID = "txt";
            box.Text = "abc";
            Init(box);
            string textBoxHtml = Render(box);

            // Button (no page -> no postback script; renders a submit input with value)
            Button button = new Button();
            button.ID = "btn";
            button.Text = "Go";
            Init(button);
            string buttonHtml = Render(button);

            // DropDownList with options
            DropDownList ddl = new DropDownList();
            ddl.ID = "ddl";
            ddl.Items.Add(new ListItem("One", "1"));
            ddl.Items.Add(new ListItem("Two", "2"));
            ddl.SelectedValue = "2";
            Init(ddl);
            string dropDownHtml = Render(ddl);

            return new object[] { labelHtml, textBoxHtml, buttonHtml, dropDownHtml };
        }

        // ---- WebControl style attributes (CssClass / BackColor) render ----
        public static string RenderStyledLabel()
        {
            Label label = new Label();
            label.ID = "styled";
            label.Text = "X";
            label.CssClass = "myClass";
            label.BackColor = global::System.Drawing.Color.Red;
            Init(label);
            return Render(label);
        }

        public sealed class GvRow { public int Id { get; set; } public string Name { get; set; } }

        // GridView smoke: bind to a List with explicit BoundFields, then auto-generate columns
        // from a second List with paging enabled. Returns object[]:
        //   [0] list row count, [1] html has "Alice", [2] html has header "The Name",
        //   [3] paged visible row count, [4] PageCount, [5] auto-gen html has "Name",
        //   [6] html starts with "<table", [7] page 0 has "x", [8] page 0 has "z"
        public static object[] GridViewSmoke()
        {
            GridView gv = new GridView();
            gv.AutoGenerateColumns = false;
            BoundField bf = new BoundField(); bf.DataField = "Name"; bf.HeaderText = "The Name"; gv.Columns.Add(bf);
            BoundField idf = new BoundField(); idf.DataField = "Id"; idf.HeaderText = "ID"; gv.Columns.Add(idf);
            Init(gv);
            System.Collections.Generic.List<GvRow> list = new System.Collections.Generic.List<GvRow>();
            list.Add(new GvRow { Id = 1, Name = "Alice" });
            list.Add(new GvRow { Id = 2, Name = "Bob" });
            gv.DataSource = list;
            gv.DataBind();
            string html = Render(gv);

            GridView gv2 = new GridView();
            gv2.AllowPaging = true; gv2.PageSize = 2;
            Init(gv2);
            System.Collections.Generic.List<GvRow> list2 = new System.Collections.Generic.List<GvRow>();
            list2.Add(new GvRow { Id = 1, Name = "x" });
            list2.Add(new GvRow { Id = 2, Name = "y" });
            list2.Add(new GvRow { Id = 3, Name = "z" });
            gv2.DataSource = list2;
            gv2.DataBind();
            string html2 = Render(gv2);

            return new object[]
            {
                gv.Rows.Count,
                html.Contains("Alice"),
                html.Contains("The Name"),
                gv2.Rows.Count,
                gv2.PageCount,
                html2.Contains("Name"),
                html2.StartsWith("<table"),
                html2.Contains(">x<"),
                html2.Contains(">z<"),
            };
        }


        // ---- Postback: TextBox / CheckBox / DropDownList LoadPostData -> change events ----
        // Returns object[]:
        //   [0] textChanged (bool)       -- TextBox.TextChanged fired
        //   [1] textValue (string)       -- TextBox.Text after postback
        //   [2] checkedChanged (bool)    -- CheckBox.CheckedChanged fired
        //   [3] isChecked (bool)         -- CheckBox.Checked after postback
        //   [4] selChanged (bool)        -- DropDownList.SelectedIndexChanged fired
        //   [5] selValue (string)        -- DropDownList.SelectedValue after postback
        public static object[] Postback()
        {
            // ----- GET -----
            ControlsPage getPage = new ControlsPage();
            CapturingWorkerRequest getWr = new CapturingWorkerRequest("GET", "/page", "", null, Array.Empty<byte>());
            HttpContext getCtx = new HttpContext(getWr);
            getPage.ProcessRequest(getCtx);
            getCtx.Response.Flush();
            string getBody = Encoding.UTF8.GetString(getWr.CapturedBody);
            string getViewState = ScrapeHidden(getBody, "__VIEWSTATE");

            // ----- POST -----
            ControlsPage postPage = new ControlsPage();
            string boxUid = postPage.BoxUid;
            string chkUid = postPage.CheckUid;
            string ddlUid = postPage.DdlUid;

            StringBuilder form = new StringBuilder();
            Append(form, "__VIEWSTATE", getViewState);
            Append(form, "__EVENTTARGET", "");
            Append(form, "__EVENTARGUMENT", "");
            Append(form, boxUid, "typed");        // change textbox value
            Append(form, chkUid, "on");           // check the checkbox
            Append(form, ddlUid, "2");            // select the second option

            byte[] body = Encoding.UTF8.GetBytes(form.ToString());
            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Content-Type", "application/x-www-form-urlencoded" },
                { "Content-Length", body.Length.ToString() }
            };
            CapturingWorkerRequest postWr = new CapturingWorkerRequest("POST", "/page", "", headers, body);
            HttpContext postCtx = new HttpContext(postWr);
            postPage.ProcessRequest(postCtx);
            postCtx.Response.Flush();

            return new object[]
            {
                postPage.TextChangedFired,
                postPage.Box.Text,
                postPage.CheckedChangedFired,
                postPage.Check.Checked,
                postPage.SelChangedFired,
                postPage.Ddl.SelectedValue,
            };
        }

        private static void Append(StringBuilder sb, string key, string value)
        {
            if (sb.Length > 0) { sb.Append('&'); }
            sb.Append(Uri.EscapeDataString(key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(value ?? string.Empty));
        }

        private static string ScrapeHidden(string html, string name)
        {
            string needle = "name=\"" + name + "\"";
            int idx = html.IndexOf(needle, StringComparison.Ordinal);
            if (idx < 0) { return null; }
            int valIdx = html.IndexOf("value=\"", idx, StringComparison.Ordinal);
            if (valIdx < 0) { return null; }
            valIdx += "value=\"".Length;
            int end = html.IndexOf('"', valIdx);
            if (end < 0) { return null; }
            string raw = html.Substring(valIdx, end - valIdx);
            return raw.Replace("&quot;", "\"").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");
        }
    }

    // A page hosting an HtmlForm with a TextBox, CheckBox and DropDownList. Wires the change
    // events so the postback worker can observe them. The HtmlForm also exercises the
    // reconciled view-state-inside-form emission path.
    internal sealed class ControlsPage : Page
    {
        private HtmlForm _form;
        private TextBox _box;
        private CheckBox _check;
        private DropDownList _ddl;
        public bool TextChangedFired;
        public bool CheckedChangedFired;
        public bool SelChangedFired;

        protected internal override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            _form = new HtmlForm();
            _form.ID = "form1";
            Controls.Add(_form);

            _box = new TextBox();
            _box.ID = "box";
            _box.AutoPostBack = true;     // forces RegisterRequiresPostBack so LoadPostData runs
            _box.TextChanged += (s, a) => { TextChangedFired = true; };
            _form.Controls.Add(_box);

            _check = new CheckBox();
            _check.ID = "chk";
            _check.AutoPostBack = true;
            _check.CheckedChanged += (s, a) => { CheckedChangedFired = true; };
            _form.Controls.Add(_check);

            _ddl = new DropDownList();
            _ddl.ID = "ddl";
            _ddl.AutoPostBack = true;
            _ddl.Items.Add(new ListItem("One", "1"));
            _ddl.Items.Add(new ListItem("Two", "2"));
            _ddl.SelectedIndexChanged += (s, a) => { SelChangedFired = true; };
            _form.Controls.Add(_ddl);
        }

        private void EnsureBuilt() { if (_form == null) { OnInit(EventArgs.Empty); } }
        public TextBox Box { get { EnsureBuilt(); return _box; } }
        public CheckBox Check { get { EnsureBuilt(); return _check; } }
        public DropDownList Ddl { get { EnsureBuilt(); return _ddl; } }
        public string BoxUid { get { EnsureBuilt(); return _box.UniqueID; } }
        public string CheckUid { get { EnsureBuilt(); return _check.UniqueID; } }
        public string DdlUid { get { EnsureBuilt(); return _ddl.UniqueID; } }
    }
}