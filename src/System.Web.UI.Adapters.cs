// clean-room System.Web.UI.Adapters implementation.
#nullable disable
#pragma warning disable

namespace System.Web.UI.Adapters
{
    public abstract class ControlAdapter
    {
        // The control this adapter is attached to. Set by the control infrastructure once
        // the adapter is resolved for a request. Kept internal so derived adapters reach it
        // only through the protected Control property.
        internal global::System.Web.UI.Control _adaptedControl;

        protected ControlAdapter() { }

        internal void SetControlInternal(global::System.Web.UI.Control control)
        {
            _adaptedControl = control;
        }

        // The default adapter behavior is to defer to the corresponding member on the
        // adapted control, so a control with an attached adapter behaves identically to one
        // without unless the adapter overrides a specific stage.
        protected internal virtual void OnInit(global::System.EventArgs e)
        {
            if (_adaptedControl != null) { _adaptedControl.OnInit(e); }
        }
        protected internal virtual void OnLoad(global::System.EventArgs e)
        {
            if (_adaptedControl != null) { _adaptedControl.OnLoad(e); }
        }
        protected internal virtual void OnPreRender(global::System.EventArgs e)
        {
            if (_adaptedControl != null) { _adaptedControl.OnPreRender(e); }
        }
        protected internal virtual void Render(global::System.Web.UI.HtmlTextWriter writer)
        {
            RenderChildren(writer);
        }
        protected virtual void RenderChildren(global::System.Web.UI.HtmlTextWriter writer)
        {
            if (_adaptedControl != null) { _adaptedControl.RenderChildren(writer); }
        }
        protected internal virtual void OnUnload(global::System.EventArgs e)
        {
            if (_adaptedControl != null) { _adaptedControl.OnUnload(e); }
        }
        protected internal virtual void BeginRender(global::System.Web.UI.HtmlTextWriter writer)
        {
            // The default rendering frame is transparent; specialized device adapters may
            // emit a container element here.
        }
        protected internal virtual void CreateChildControls()
        {
            if (_adaptedControl != null) { _adaptedControl.CreateChildControls(); }
        }
        protected internal virtual void EndRender(global::System.Web.UI.HtmlTextWriter writer)
        {
        }
        protected internal virtual void LoadAdapterControlState(global::System.Object state) { }
        protected internal virtual void LoadAdapterViewState(global::System.Object state) { }
        protected internal virtual global::System.Object SaveAdapterControlState() { return null; }
        protected internal virtual global::System.Object SaveAdapterViewState() { return null; }

        protected global::System.Web.UI.Control Control { get { return _adaptedControl; } }
        protected global::System.Web.UI.Page Page { get { return _adaptedControl == null ? null : _adaptedControl.Page; } }
        protected global::System.Web.UI.Adapters.PageAdapter PageAdapter
        {
            get
            {
                global::System.Web.UI.Page p = Page;
                return p == null ? null : p.PageAdapter;
            }
        }
        protected global::System.Web.HttpBrowserCapabilities Browser
        {
            get
            {
                global::System.Web.HttpContext ctx = global::System.Web.HttpContext.Current;
                if (ctx == null) { return null; }
                global::System.Web.HttpRequest req = ctx.Request;
                return req == null ? null : req.Browser;
            }
        }
    }
    public abstract class PageAdapter : global::System.Web.UI.Adapters.ControlAdapter
    {
        protected PageAdapter() { }

        // The adapted control for a page adapter is the Page itself.
        private global::System.Web.UI.Page AdaptedPage { get { return _adaptedControl as global::System.Web.UI.Page; } }

        public virtual global::System.Collections.Specialized.NameValueCollection DeterminePostBackMode()
        {
            global::System.Web.HttpContext ctx = global::System.Web.HttpContext.Current;
            if (ctx == null) { return null; }
            global::System.Web.HttpRequest req = ctx.Request;
            if (req == null) { return null; }
            if (global::System.String.Equals(req.HttpMethod, "POST", global::System.StringComparison.OrdinalIgnoreCase))
            {
                return req.Form;
            }
            return req.QueryString;
        }
        public virtual global::System.Collections.Specialized.NameValueCollection DeterminePostBackModeUnvalidated()
        {
            global::System.Web.HttpContext ctx = global::System.Web.HttpContext.Current;
            if (ctx == null) { return null; }
            global::System.Web.HttpRequest req = ctx.Request;
            if (req == null) { return null; }
            global::System.Web.UnvalidatedRequestValues u = req.Unvalidated;
            if (global::System.String.Equals(req.HttpMethod, "POST", global::System.StringComparison.OrdinalIgnoreCase))
            {
                return u == null ? req.Form : u.Form;
            }
            return u == null ? req.QueryString : u.QueryString;
        }
        public virtual global::System.Collections.ICollection GetRadioButtonsByGroup(global::System.String groupName)
        {
            // Device-targeted grouping is not maintained by this adapter; callers receive an
            // empty collection rather than null.
            return new global::System.Collections.ArrayList();
        }
        protected internal virtual global::System.String GetPostBackFormReference(global::System.String formId)
        {
            // Default client-side form reference used by emitted postback script.
            return "document.forms['" + formId + "']";
        }
        public virtual global::System.Web.UI.PageStatePersister GetStatePersister()
        {
            global::System.Web.UI.Page p = AdaptedPage;
            if (p == null) { return null; }
            return new global::System.Web.UI.HiddenFieldPageStatePersister(p);
        }
        public virtual void RegisterRadioButton(global::System.Web.UI.WebControls.RadioButton radioButton)
        {
            // No device-specific radio-button tracking is required for the default adapter.
        }
        public virtual void RenderBeginHyperlink(global::System.Web.UI.HtmlTextWriter writer, global::System.String targetUrl, global::System.Boolean encodeUrl, global::System.String softkeyLabel)
        {
            RenderBeginHyperlink(writer, targetUrl, encodeUrl, softkeyLabel, null);
        }
        public virtual void RenderBeginHyperlink(global::System.Web.UI.HtmlTextWriter writer, global::System.String targetUrl, global::System.Boolean encodeUrl, global::System.String softkeyLabel, global::System.String accessKey)
        {
            if (writer == null) { throw new global::System.ArgumentNullException("writer"); }
            global::System.String url = targetUrl;
            if (encodeUrl && url != null)
            {
                url = global::System.Web.HttpUtility.HtmlAttributeEncode(url);
            }
            if (accessKey != null && accessKey.Length > 0)
            {
                writer.WriteBeginTag("a");
                writer.WriteAttribute("accesskey", accessKey);
                writer.WriteAttribute("href", url, false);
                writer.Write('>');
            }
            else
            {
                writer.WriteBeginTag("a");
                writer.WriteAttribute("href", url, false);
                writer.Write('>');
            }
        }
        public virtual void RenderEndHyperlink(global::System.Web.UI.HtmlTextWriter writer)
        {
            if (writer == null) { throw new global::System.ArgumentNullException("writer"); }
            writer.WriteEndTag("a");
        }
        public virtual void RenderPostBackEvent(global::System.Web.UI.HtmlTextWriter writer, global::System.String target, global::System.String argument, global::System.String softkeyLabel, global::System.String text)
        {
            RenderPostBackEvent(writer, target, argument, softkeyLabel, text, null, null);
        }
        public virtual void RenderPostBackEvent(global::System.Web.UI.HtmlTextWriter writer, global::System.String target, global::System.String argument, global::System.String softkeyLabel, global::System.String text, global::System.String postUrl, global::System.String accessKey)
        {
            RenderPostBackEvent(writer, target, argument, softkeyLabel, text, postUrl, accessKey, true);
        }
        protected void RenderPostBackEvent(global::System.Web.UI.HtmlTextWriter writer, global::System.String target, global::System.String argument, global::System.String softkeyLabel, global::System.String text, global::System.String postUrl, global::System.String accessKey, global::System.Boolean encode)
        {
            if (writer == null) { throw new global::System.ArgumentNullException("writer"); }
            global::System.Web.UI.Page p = AdaptedPage;
            global::System.String script = (p != null)
                ? p.ClientScript.GetPostBackEventReference(new global::System.Web.UI.PostBackOptions(p, argument))
                : ("__doPostBack('" + target + "','" + argument + "')");

            global::System.String href = "javascript:" + script;
            writer.WriteBeginTag("a");
            if (accessKey != null && accessKey.Length > 0) { writer.WriteAttribute("accesskey", accessKey); }
            writer.WriteAttribute("href", encode ? global::System.Web.HttpUtility.HtmlAttributeEncode(href) : href, false);
            writer.Write('>');
            if (text != null) { writer.Write(text); }
            writer.WriteEndTag("a");
        }
        public virtual global::System.String TransformText(global::System.String text)
        {
            // The default adapter performs no device-specific text transformation.
            return text;
        }
        public virtual global::System.Collections.Specialized.StringCollection CacheVaryByHeaders { get { return null; } }
        public virtual global::System.Collections.Specialized.StringCollection CacheVaryByParams { get { return null; } }
        protected global::System.String ClientState
        {
            get
            {
                // The serialized client view-state string is produced by the page state
                // persister during render; it is not exposed as a public Page member in this
                // implementation, so the default adapter reports null.
                return null;
            }
        }
    }
}
