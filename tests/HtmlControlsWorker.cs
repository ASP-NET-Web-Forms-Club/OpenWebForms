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
    // Worker executed INSIDE the custom AssemblyLoadContext (via RunInAlc), so the HtmlControls
    // base types bind to OUR clean-room System.Web rather than the shared-framework facade.
    //
    //   * RENDER: HtmlInputText / HtmlAnchor / HtmlGenericControl rendered via HtmlTextWriter.
    //   * FORM:   a full Page GET whose HtmlForm must emit the page __VIEWSTATE hidden field
    //             INSIDE the <form> (the Tier-4 page-level workaround is reconciled).
    //   * POSTBACK: HtmlSelect LoadPostData -> ServerChange round-trip via ProcessRequest.
    internal static class HtmlControlsWorker
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

        // Returns object[]: [0] inputTextHtml, [1] anchorHtml, [2] genericHtml
        public static object[] RenderControls()
        {
            HtmlInputText text = new HtmlInputText();
            text.ID = "name";
            text.Value = "Bob";
            text.MaxLength = 10;
            Init(text);
            string inputTextHtml = Render(text);

            HtmlAnchor anchor = new HtmlAnchor();
            anchor.ID = "lnk";
            anchor.HRef = "http://example.com/";
            anchor.Title = "Example";
            anchor.InnerText = "click";
            Init(anchor);
            string anchorHtml = Render(anchor);

            HtmlGenericControl generic = new HtmlGenericControl("section");
            generic.ID = "sec";
            generic.Attributes["data-role"] = "main";
            generic.InnerHtml = "<p>hi</p>";
            Init(generic);
            string genericHtml = Render(generic);

            return new object[] { inputTextHtml, anchorHtml, genericHtml };
        }

        // Full Page GET: the HtmlForm must render <form> ... with __VIEWSTATE inside it.
        // Returns object[]: [0] body (string), [1] viewStateInsideForm (bool)
        public static object[] FormRendersViewState()
        {
            FormPage page = new FormPage();
            CapturingWorkerRequest wr = new CapturingWorkerRequest("GET", "/page", "", null, Array.Empty<byte>());
            HttpContext ctx = new HttpContext(wr);
            page.ProcessRequest(ctx);
            ctx.Response.Flush();
            string body = Encoding.UTF8.GetString(wr.CapturedBody);

            // __VIEWSTATE must appear between the <form ...> and the </form>.
            int formOpen = body.IndexOf("<form", StringComparison.Ordinal);
            int formClose = body.IndexOf("</form>", StringComparison.Ordinal);
            int vs = body.IndexOf("name=\"__VIEWSTATE\"", StringComparison.Ordinal);
            bool inside = formOpen >= 0 && formClose > formOpen && vs > formOpen && vs < formClose;

            // It must appear exactly once (no double emission at page level + form level).
            int count = 0; int idx = 0;
            while ((idx = body.IndexOf("name=\"__VIEWSTATE\"", idx, StringComparison.Ordinal)) >= 0) { count++; idx += 1; }
            bool exactlyOnce = count == 1;

            return new object[] { body, inside && exactlyOnce };
        }

        // HtmlSelect postback: posting a value selects that option and fires ServerChange.
        // Returns object[]: [0] serverChangeFired (bool), [1] selectedValue (string)
        public static object[] SelectPostback()
        {
            // ----- GET -----
            SelectPage getPage = new SelectPage();
            CapturingWorkerRequest getWr = new CapturingWorkerRequest("GET", "/page", "", null, Array.Empty<byte>());
            HttpContext getCtx = new HttpContext(getWr);
            getPage.ProcessRequest(getCtx);
            getCtx.Response.Flush();
            string getBody = Encoding.UTF8.GetString(getWr.CapturedBody);
            string getViewState = ScrapeHidden(getBody, "__VIEWSTATE");

            // ----- POST -----
            SelectPage postPage = new SelectPage();
            string selUid = postPage.SelectUid;
            StringBuilder form = new StringBuilder();
            Append(form, "__VIEWSTATE", getViewState);
            Append(form, "__EVENTTARGET", "");
            Append(form, "__EVENTARGUMENT", "");
            Append(form, selUid, "b");   // select option with value "b" (index 1)
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

            return new object[] { postPage.ServerChangeFired, postPage.Select.Value };
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

    // A page with just an HtmlForm so the form-emission path is exercised.
    internal sealed class FormPage : Page
    {
        protected internal override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            HtmlForm form = new HtmlForm();
            form.ID = "form1";
            Controls.Add(form);
            HtmlGenericControl div = new HtmlGenericControl("div");
            div.InnerText = "content";
            form.Controls.Add(div);
        }
    }

    // A page hosting an HtmlForm with an HtmlSelect, wired for postback observation.
    internal sealed class SelectPage : Page
    {
        private HtmlForm _form;
        private HtmlSelect _select;
        public bool ServerChangeFired;

        protected internal override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            _form = new HtmlForm();
            _form.ID = "form1";
            Controls.Add(_form);

            _select = new HtmlSelect();
            _select.ID = "sel";
            _select.Items.Add(new ListItem("Alpha", "a"));
            _select.Items.Add(new ListItem("Beta", "b"));
            _select.ServerChange += (s, a) => { ServerChangeFired = true; };
            _form.Controls.Add(_select);
        }

        private void EnsureBuilt() { if (_form == null) { OnInit(EventArgs.Empty); } }
        public HtmlSelect Select { get { EnsureBuilt(); return _select; } }
        public string SelectUid { get { EnsureBuilt(); return _select.UniqueID; } }
    }
}
