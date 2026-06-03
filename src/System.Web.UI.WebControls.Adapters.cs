// clean-room System.Web.UI.WebControls.Adapters implementation.
#nullable disable
#pragma warning disable

namespace System.Web.UI.WebControls.Adapters
{
    public class WebControlAdapter : global::System.Web.UI.Adapters.ControlAdapter
    {
        public WebControlAdapter() { }

        // The adapted WebControl; the base ControlAdapter stores the generic Control, this
        // strongly-typed view casts it for derived adapters.
        protected new global::System.Web.UI.WebControls.WebControl Control
        {
            get { return _adaptedControl as global::System.Web.UI.WebControls.WebControl; }
        }

        // Mirrors WebControl.IsEnabled so adapters can suppress interactive markup for
        // disabled controls without reaching the protected member directly.
        protected global::System.Boolean IsEnabled
        {
            get
            {
                global::System.Web.UI.WebControls.WebControl wc = Control;
                return wc != null && wc.Enabled;
            }
        }

        protected virtual void RenderBeginTag(global::System.Web.UI.HtmlTextWriter writer)
        {
            global::System.Web.UI.WebControls.WebControl wc = Control;
            if (wc != null) { wc.RenderBeginTag(writer); }
        }
        protected virtual void RenderEndTag(global::System.Web.UI.HtmlTextWriter writer)
        {
            global::System.Web.UI.WebControls.WebControl wc = Control;
            if (wc != null) { wc.RenderEndTag(writer); }
        }
        protected virtual void RenderContents(global::System.Web.UI.HtmlTextWriter writer)
        {
            global::System.Web.UI.WebControls.WebControl wc = Control;
            if (wc != null) { wc.RenderContents(writer); }
        }
        protected internal override void Render(global::System.Web.UI.HtmlTextWriter writer)
        {
            RenderBeginTag(writer);
            RenderContents(writer);
            RenderEndTag(writer);
        }
    }
    public class DataBoundControlAdapter : global::System.Web.UI.WebControls.Adapters.WebControlAdapter
    {
        public DataBoundControlAdapter() { }

        protected internal virtual void PerformDataBinding(global::System.Collections.IEnumerable data)
        {
            global::System.Web.UI.WebControls.DataBoundControl c = Control;
            if (c != null) { c.PerformDataBinding(data); }
        }
        protected new global::System.Web.UI.WebControls.DataBoundControl Control
        {
            get { return _adaptedControl as global::System.Web.UI.WebControls.DataBoundControl; }
        }
    }
    public class HideDisabledControlAdapter : global::System.Web.UI.WebControls.Adapters.WebControlAdapter
    {
        public HideDisabledControlAdapter() { }

        protected internal override void Render(global::System.Web.UI.HtmlTextWriter writer)
        {
            // Devices that cannot grey out a disabled control simply omit it from the markup.
            if (IsEnabled) { base.Render(writer); }
        }
    }
    public class HierarchicalDataBoundControlAdapter : global::System.Web.UI.WebControls.Adapters.WebControlAdapter
    {
        public HierarchicalDataBoundControlAdapter() { }

        protected internal virtual void PerformDataBinding()
        {
            global::System.Web.UI.WebControls.HierarchicalDataBoundControl c = Control;
            if (c != null) { c.PerformDataBinding(); }
        }
        protected new global::System.Web.UI.WebControls.HierarchicalDataBoundControl Control
        {
            get { return _adaptedControl as global::System.Web.UI.WebControls.HierarchicalDataBoundControl; }
        }
    }
    public class MenuAdapter : global::System.Web.UI.WebControls.Adapters.WebControlAdapter, global::System.Web.UI.IPostBackEventHandler
    {
        public MenuAdapter() { }

        protected internal override void LoadAdapterControlState(global::System.Object state)
        {
            // The adapter keeps no control state of its own beyond what the Menu persists.
        }
        protected internal override void OnInit(global::System.EventArgs e)
        {
            base.OnInit(e);
        }
        protected internal override void OnPreRender(global::System.EventArgs e)
        {
            base.OnPreRender(e);
        }
        protected internal override global::System.Object SaveAdapterControlState()
        {
            return null;
        }
        protected override void RenderBeginTag(global::System.Web.UI.HtmlTextWriter writer)
        {
            base.RenderBeginTag(writer);
        }
        protected override void RenderContents(global::System.Web.UI.HtmlTextWriter writer)
        {
            base.RenderContents(writer);
        }
        protected override void RenderEndTag(global::System.Web.UI.HtmlTextWriter writer)
        {
            base.RenderEndTag(writer);
        }
        protected internal virtual void RenderItem(global::System.Web.UI.HtmlTextWriter writer, global::System.Web.UI.WebControls.MenuItem item, global::System.Int32 position)
        {
            if (writer == null || item == null) { return; }
            // Minimal item rendering: emit the item text (or a navigation anchor) so a
            // device adapter without a richer template still produces usable output.
            global::System.String text = item.Text;
            global::System.String url = item.NavigateUrl;
            if (url != null && url.Length > 0)
            {
                writer.WriteBeginTag("a");
                writer.WriteAttribute("href", url, true);
                writer.Write('>');
                if (text != null) { writer.Write(text); }
                writer.WriteEndTag("a");
            }
            else if (text != null)
            {
                writer.Write(text);
            }
        }
        void global::System.Web.UI.IPostBackEventHandler.RaisePostBackEvent(global::System.String eventArgument)
        {
            RaisePostBackEvent(eventArgument);
        }
        protected virtual void RaisePostBackEvent(global::System.String eventArgument)
        {
            // Forward menu navigation/selection postbacks to the adapted Menu, which owns the
            // IPostBackEventHandler contract.
            global::System.Web.UI.WebControls.Menu m = Control;
            global::System.Web.UI.IPostBackEventHandler h = m as global::System.Web.UI.IPostBackEventHandler;
            if (h != null) { h.RaisePostBackEvent(eventArgument); }
        }
        protected new global::System.Web.UI.WebControls.Menu Control
        {
            get { return _adaptedControl as global::System.Web.UI.WebControls.Menu; }
        }
    }
}
