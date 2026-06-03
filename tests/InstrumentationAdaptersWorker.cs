using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Instrumentation;
using System.Web.UI;
using System.Web.UI.Adapters;
using System.Web.UI.WebControls.Adapters;

namespace System.Web.Tests
{
    // Workers executed INSIDE the custom AssemblyLoadContext so the PageInstrumentationService,
    // the PageExecutionListener base class and the control-adapter render pipeline bind to OUR
    // clean-room System.Web. These are light coverage checks for the instrumentation listener
    // dispatch contract and the adapter render delegation order. Workers return primitives across
    // the boundary.
    //
    // ControlAdapter.Render is protected internal: "internal" scopes to System.Web so the test
    // assembly cannot call it as internal, but "protected" lets a DERIVED type invoke base.Render
    // from its own member. Each adapter subclass below therefore exposes a public RenderThrough()
    // that calls base.Render(writer) from inside the derived type.
    internal static class InstrumentationAdaptersWorker
    {
        // ----- instrumentation ----------------------------------------------------------

        private sealed class RecordingListener : PageExecutionListener
        {
            public int BeginCount;
            public int EndCount;
            public string LastVirtualPath;
            public bool LastIsLiteral;

            public override void BeginContext(PageExecutionContext context)
            {
                BeginCount++;
                if (context != null) { LastVirtualPath = context.VirtualPath; LastIsLiteral = context.IsLiteral; }
            }
            public override void EndContext(PageExecutionContext context)
            {
                EndCount++;
            }
        }

        // Register a listener on a PageInstrumentationService and drive a begin/end context pair
        // through it, mirroring what the render pipeline does for an instrumented region.
        // Returns:
        //   [0] listener registered through the service list (bool) true
        //   [1] BeginContext fired exactly once               (bool) true
        //   [2] EndContext fired exactly once                 (bool) true
        //   [3] context payload round-tripped                 (bool) true
        public static object[] InstrumentationListenerFires()
        {
            PageInstrumentationService service = new PageInstrumentationService();
            PageInstrumentationService.IsEnabled = true;

            RecordingListener listener = new RecordingListener();
            service.ExecutionListeners.Add(listener);
            bool registered = service.ExecutionListeners.Count == 1
                && ReferenceEquals(service.ExecutionListeners[0], listener);

            PageExecutionContext context = new PageExecutionContext();
            context.VirtualPath = "~/Default.aspx";
            context.IsLiteral = true;
            context.Length = 12;
            context.StartPosition = 0;
            context.TextWriter = new StringWriter();

            foreach (PageExecutionListener l in service.ExecutionListeners)
            {
                l.BeginContext(context);
            }
            foreach (PageExecutionListener l in service.ExecutionListeners)
            {
                l.EndContext(context);
            }

            return new object[]
            {
                registered,
                listener.BeginCount == 1,
                listener.EndCount == 1,
                listener.LastVirtualPath == "~/Default.aspx" && listener.LastIsLiteral,
            };
        }

        // ----- adapter render delegation ------------------------------------------------

        // The base ControlAdapter.Render defers to RenderChildren; a derived adapter that
        // overrides RenderChildren must see it called when Render runs.
        private sealed class ChildRenderAdapter : ControlAdapter
        {
            public bool ChildrenRendered;
            protected override void RenderChildren(HtmlTextWriter writer)
            {
                ChildrenRendered = true;
                if (writer != null) { writer.Write("[children]"); }
            }
            public void RenderThrough(HtmlTextWriter writer) { base.Render(writer); }
        }

        // A WebControlAdapter whose Render must invoke RenderBeginTag, then RenderContents, then
        // RenderEndTag in that order; overriding the three protected hooks captures the order
        // without needing an attached WebControl.
        private sealed class OrderingWebControlAdapter : WebControlAdapter
        {
            public readonly List<string> Calls = new List<string>();
            protected override void RenderBeginTag(HtmlTextWriter writer) { Calls.Add("begin"); }
            protected override void RenderContents(HtmlTextWriter writer) { Calls.Add("contents"); }
            protected override void RenderEndTag(HtmlTextWriter writer) { Calls.Add("end"); }
            public void RenderThrough(HtmlTextWriter writer) { base.Render(writer); }
        }

        // Returns:
        //   [0] base ControlAdapter.Render delegated to RenderChildren (bool) true
        //   [1] rendered output captured                               (string) "[children]"
        //   [2] WebControlAdapter.Render order == begin,contents,end    (bool) true
        public static object[] AdapterDelegatesRender()
        {
            ChildRenderAdapter childAdapter = new ChildRenderAdapter();
            StringWriter sw = new StringWriter();
            HtmlTextWriter writer = new HtmlTextWriter(sw);
            childAdapter.RenderThrough(writer);
            writer.Flush();
            string childOutput = sw.ToString();

            OrderingWebControlAdapter wcAdapter = new OrderingWebControlAdapter();
            StringWriter sw2 = new StringWriter();
            HtmlTextWriter writer2 = new HtmlTextWriter(sw2);
            wcAdapter.RenderThrough(writer2);
            bool order = wcAdapter.Calls.Count == 3
                && wcAdapter.Calls[0] == "begin"
                && wcAdapter.Calls[1] == "contents"
                && wcAdapter.Calls[2] == "end";

            return new object[]
            {
                childAdapter.ChildrenRendered,
                childOutput,
                order,
            };
        }
    }
}
