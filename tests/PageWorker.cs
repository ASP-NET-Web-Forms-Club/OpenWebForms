using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Web;
using System.Web.UI;

namespace System.Web.Tests
{
    // Worker executed INSIDE the custom AssemblyLoadContext (via RunInAlc). Because this
    // code runs in the ALC, the base types Page/Control bind to OUR clean-room System.Web.
    //
    // It drives a full GET -> POST postback round-trip against a hand-written Page:
    //   * GET renders the page; we scrape the emitted __VIEWSTATE hidden field.
    //   * POST replays that __VIEWSTATE plus __EVENTTARGET pointing at a control that
    //     implements IPostBackEventHandler and persists control state. The event handler
    //     mutates the counter; the re-saved __VIEWSTATE must reflect the mutated value.
    //
    // Returns object[]:
    //   [0] getBody (string)          -- full GET response body
    //   [1] getViewState (string)     -- scraped __VIEWSTATE from GET
    //   [2] postViewState (string)    -- scraped __VIEWSTATE from POST
    //   [3] counterAfterPost (int)    -- the control's counter after the raised event
    //   [4] counterRestoredOnPost (int) -- counter value restored from control state on POST (before event)
    //   [5] textboxValueOnPost (string) -- IPostBackDataHandler-loaded value
    //   [6] dataChangedFired (bool)   -- whether RaisePostDataChangedEvent fired
    //   [7] eventRaised (bool)        -- whether RaisePostBackEvent fired
    internal static class PageWorker
    {
        public static object[] RoundTrip()
        {
            // ----- GET -----
            CounterPage getPage = new CounterPage();
            CapturingWorkerRequest getWr = new CapturingWorkerRequest("GET", "/page", "", null, Array.Empty<byte>());
            HttpContext getCtx = new HttpContext(getWr);
            getPage.ProcessRequest(getCtx);
            getCtx.Response.Flush();
            string getBody = Encoding.UTF8.GetString(getWr.CapturedBody);
            string getViewState = ScrapeHidden(getBody, "__VIEWSTATE");

            // ----- POST -----
            // Build an application/x-www-form-urlencoded body replaying __VIEWSTATE +
            // __EVENTTARGET (the counter control) + the textbox's posted value.
            CounterPage postPage = new CounterPage();
            string targetUid = postPage.CounterControlUniqueId; // "ctl00" style; deterministic
            string boxUid = postPage.BoxControlUniqueId;
            StringBuilder form = new StringBuilder();
            Append(form, "__VIEWSTATE", getViewState);
            Append(form, "__EVENTTARGET", targetUid);
            Append(form, "__EVENTARGUMENT", "");
            Append(form, boxUid, "typed-value");
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
            string postBody = Encoding.UTF8.GetString(postWr.CapturedBody);
            string postViewState = ScrapeHidden(postBody, "__VIEWSTATE");

            return new object[]
            {
                getBody,
                getViewState,
                postViewState,
                postPage.Counter,
                postPage.RestoredCounter,
                postPage.Box.LoadedValue,
                postPage.Box.DataChangedFired,
                postPage.EventRaisedFlag,
            };
        }

        private static void Append(StringBuilder sb, string key, string value)
        {
            if (sb.Length > 0) { sb.Append('&'); }
            sb.Append(Uri.EscapeDataString(key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(value ?? string.Empty));
        }

        // Minimal HTML scrape of <input ... name="X" ... value="Y" ... /> -> Y (HTML-attr decoded).
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
            // Minimal HTML-attribute decode (values are base64/uids; only & " < > may be encoded).
            return raw.Replace("&quot;", "\"").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");
        }
    }

    // A page that builds a fixed control tree: a counter control (control state + postback
    // event handler) and a textbox (postback data handler + view state).
    internal sealed class CounterPage : Page
    {
        private CounterControl _counter;
        private TextBoxControl _box;
        public bool EventRaisedFlag;

        protected internal override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            _counter = new CounterControl();
            _counter.ID = "counter";
            _box = new TextBoxControl();
            _box.ID = "box";
            Controls.Add(_counter);
            Controls.Add(_box);
            _counter.RaisedEvent += (s, a) => { EventRaisedFlag = true; };
        }

        public CounterControl CounterCtl { get { EnsureBuilt(); return _counter; } }
        public TextBoxControl Box { get { EnsureBuilt(); return _box; } }
        public int Counter { get { return _counter != null ? _counter.Count : -1; } }
        public int RestoredCounter { get { return _counter != null ? _counter.RestoredCount : -1; } }
        public string CounterControlUniqueId { get { EnsureBuilt(); return _counter.UniqueID; } }
        public string BoxControlUniqueId { get { EnsureBuilt(); return _box.UniqueID; } }

        private void EnsureBuilt()
        {
            if (_counter == null) { OnInit(EventArgs.Empty); }
        }
    }

    // Control that persists an integer Count via CONTROL STATE and increments it whenever
    // its postback event is raised. Registers for control state during Init.
    internal sealed class CounterControl : Control, IPostBackEventHandler
    {
        private int _count;
        public int RestoredCount = -1;
        public event EventHandler RaisedEvent;

        public int Count { get { return _count; } set { _count = value; } }

        protected internal override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            Page.RegisterRequiresControlState(this);
        }

        protected internal override object SaveControlState()
        {
            // Pre-increment by render time the count is whatever the event left it at.
            return _count;
        }

        protected internal override void LoadControlState(object savedState)
        {
            if (savedState is int)
            {
                _count = (int)savedState;
                RestoredCount = _count;
            }
        }

        public void RaisePostBackEvent(string eventArgument)
        {
            _count++;
            if (RaisedEvent != null) { RaisedEvent(this, EventArgs.Empty); }
        }

        protected internal override void Render(HtmlTextWriter writer)
        {
            writer.Write("<span id=\"" + ClientID + "\">count=" + _count + "</span>");
        }
    }

    // Control that loads a posted value via IPostBackDataHandler and stores it in view state.
    internal sealed class TextBoxControl : Control, IPostBackDataHandler
    {
        public string LoadedValue;
        public bool DataChangedFired;

        public string Text
        {
            get { object o = ViewState["Text"]; return o == null ? string.Empty : (string)o; }
            set { ViewState["Text"] = value; }
        }

        public bool LoadPostData(string postDataKey, NameValueCollection postCollection)
        {
            string posted = postCollection[postDataKey];
            LoadedValue = posted;
            string current = Text;
            if (!string.Equals(current, posted, StringComparison.Ordinal))
            {
                Text = posted;
                return true;
            }
            return false;
        }

        public void RaisePostDataChangedEvent()
        {
            DataChangedFired = true;
        }

        protected internal override void Render(HtmlTextWriter writer)
        {
            string v = Text.Replace("&", "&amp;").Replace("\"", "&quot;");
            writer.Write("<input type=\"text\" name=\"" + UniqueID + "\" value=\"" + v + "\" />");
        }
    }
}
