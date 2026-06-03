using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier 10 (System.Web.Management) health-monitoring tests. Each scenario runs inside the
    // custom AssemblyLoadContext so the WebBaseEvent hierarchy, the buffered web-event provider
    // base class and the WebEventFormatter bind to OUR clean-room System.Web. Deterministic and
    // cross-platform: the providers used here capture events in memory rather than touching the
    // OS event log / SMTP / SQL.
    public class ManagementTests
    {
        private static SystemWebUnderTest SW => SystemWebUnderTest.Instance;

        [Fact]
        public void BufferedProvider_ReceivesRaisedRequestError_AndFormatsToNonEmptyString()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.ManagementWorker", "BufferedProviderReceivesRequestError");
            Assert.True((bool)r[0]); // provider received exactly one event
            Assert.True((bool)r[1]); // WebBaseEvent formatted text is non-empty
            Assert.True((bool)r[2]); // formatted text includes the event message
            Assert.True((bool)r[3]); // ErrorException round-trips on the error event
        }

        [Fact]
        public void HeartbeatEvent_DispatchesToProvider_AndFormatsProcessStatistics()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.ManagementWorker", "HeartbeatEventFormatsAndDispatches");
            Assert.True((bool)r[0]);  // heartbeat reached the provider
            Assert.True((bool)r[1]);  // formatted text non-empty
            Assert.True((bool)r[2]);  // includes the heartbeat process-statistics block
            Assert.Equal(1005, r[3]); // WebEventCodes.ApplicationHeartbeat
        }

        [Fact]
        public void BufferedProvider_WithBufferingEnabled_HoldsEventsUntilFlush()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.ManagementWorker", "BufferingHoldsUntilFlush");
            Assert.True((bool)r[0]); // nothing delivered before Flush
            Assert.True((bool)r[1]); // both events delivered on Flush
        }
    }
}
