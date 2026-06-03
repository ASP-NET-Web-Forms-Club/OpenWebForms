// clean-room System.Web.Instrumentation implementation.
#nullable disable
#pragma warning disable

namespace System.Web.Instrumentation
{
    public sealed class PageInstrumentationService
    {
        private static global::System.Boolean _isEnabled;
        private global::System.Collections.Generic.IList<global::System.Web.Instrumentation.PageExecutionListener> _listeners;

        public PageInstrumentationService() { }

        // Page instrumentation is globally toggled; the rendering pipeline consults this
        // flag before allocating execution contexts and dispatching to listeners.
        public static global::System.Boolean IsEnabled
        {
            get { return _isEnabled; }
            set { _isEnabled = value; }
        }

        public global::System.Collections.Generic.IList<global::System.Web.Instrumentation.PageExecutionListener> ExecutionListeners
        {
            get
            {
                if (_listeners == null)
                {
                    _listeners = new global::System.Collections.Generic.List<global::System.Web.Instrumentation.PageExecutionListener>();
                }
                return _listeners;
            }
        }
    }
    public class PageExecutionContext
    {
        public PageExecutionContext() { }

        // True when the described region is static literal markup (as opposed to a
        // control- or code-generated fragment).
        public global::System.Boolean IsLiteral { get; set; }

        // Length, in characters, of the region within the rendered output.
        public global::System.Int32 Length { get; set; }

        // Zero-based character offset of the region within the destination writer's output.
        public global::System.Int32 StartPosition { get; set; }

        // The writer the region is being emitted to.
        public global::System.IO.TextWriter TextWriter { get; set; }

        // Virtual path of the source file (page, master, or user control) the region
        // originated from.
        public global::System.String VirtualPath { get; set; }
    }
    public abstract class PageExecutionListener
    {
        protected PageExecutionListener() { }
        public abstract void BeginContext(global::System.Web.Instrumentation.PageExecutionContext context);
        public abstract void EndContext(global::System.Web.Instrumentation.PageExecutionContext context);
    }
}
