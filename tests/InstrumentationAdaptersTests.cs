using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier 10 light coverage for System.Web.Instrumentation and the control-adapter render
    // pipeline (System.Web.UI.Adapters / System.Web.UI.WebControls.Adapters). Run inside the
    // custom AssemblyLoadContext so the instrumentation service, the listener base class and the
    // adapter base classes bind to OUR clean-room System.Web.
    public class InstrumentationAdaptersTests
    {
        private static SystemWebUnderTest SW => SystemWebUnderTest.Instance;

        [Fact]
        public void PageInstrumentationService_DispatchesToRegisteredListener()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.InstrumentationAdaptersWorker", "InstrumentationListenerFires");
            Assert.True((bool)r[0]); // listener registered via ExecutionListeners
            Assert.True((bool)r[1]); // BeginContext fired once
            Assert.True((bool)r[2]); // EndContext fired once
            Assert.True((bool)r[3]); // context payload round-tripped
        }

        [Fact]
        public void ControlAdapter_Render_DelegatesThroughRenderPipeline()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.InstrumentationAdaptersWorker", "AdapterDelegatesRender");
            Assert.True((bool)r[0]);              // base Render -> RenderChildren
            Assert.Equal("[children]", r[1]);     // child render output captured
            Assert.True((bool)r[2]);              // WebControlAdapter begin/contents/end order
        }
    }
}
