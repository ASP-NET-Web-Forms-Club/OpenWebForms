using System;
using System.Web.Management;

namespace System.Web.Tests
{
    // Workers executed INSIDE the custom AssemblyLoadContext (via RunInAlc) so the
    // System.Web.Management event hierarchy (WebBaseEvent and descendants), the buffered
    // provider base class and the WebEventFormatter all bind to OUR clean-room System.Web.
    //
    // The WebBaseEvent ctors are protected internal, so a test cannot construct an event
    // directly across the assembly boundary; instead each event type is subclassed here
    // (the subclass has access to the protected ctor through base(...)) and the public
    // ProcessEvent(...) entry point on a provider is driven directly. Buffering defaults to
    // off, so ProcessEvent immediately routes the event through ProcessEventFlush, which the
    // capturing provider records. Workers return primitives/strings across the boundary.
    internal static class ManagementWorker
    {
        // A WebRequestErrorEvent subclass exposing a public ctor over the protected-internal base.
        private sealed class TestRequestErrorEvent : WebRequestErrorEvent
        {
            public TestRequestErrorEvent(string message, object source, int eventCode, Exception e)
                : base(message, source, eventCode, e) { }
        }

        private sealed class TestHeartbeatEvent : WebHeartbeatEvent
        {
            public TestHeartbeatEvent(string message, int eventCode)
                : base(message, eventCode) { }
        }

        // A buffered provider that records the events handed to it on flush. Buffering is left
        // off (the default), so each ProcessEvent flushes a single-event notification straight
        // to ProcessEventFlush.
        private sealed class CapturingProvider : BufferedWebEventProvider
        {
            public int ReceivedCount;
            public WebBaseEvent LastEvent;
            public string LastFormatted;

            public override void ProcessEventFlush(WebEventBufferFlushInfo flushInfo)
            {
                if (flushInfo == null || flushInfo.Events == null) { return; }
                foreach (object o in flushInfo.Events)
                {
                    WebBaseEvent ev = o as WebBaseEvent;
                    if (ev == null) { continue; }
                    ReceivedCount++;
                    LastEvent = ev;
                    LastFormatted = ev.ToString(true, true);
                }
            }
        }

        // Raise a WebRequestErrorEvent into a buffered (unbuffered-mode) provider.
        // Returns:
        //   [0] provider received exactly one event (bool) true
        //   [1] the formatted event text is non-empty   (bool) true
        //   [2] the formatted text mentions the message  (bool) true
        //   [3] ErrorException round-trips               (bool) true
        public static object[] BufferedProviderReceivesRequestError()
        {
            CapturingProvider provider = new CapturingProvider();
            provider.Initialize("capture", new System.Collections.Specialized.NameValueCollection());

            Exception boom = new InvalidOperationException("kaboom");
            TestRequestErrorEvent ev = new TestRequestErrorEvent(
                "a request failed", null, WebEventCodes.RuntimeErrorRequestAbort, boom);

            provider.ProcessEvent(ev);

            string formatted = provider.LastFormatted;
            WebRequestErrorEvent captured = provider.LastEvent as WebRequestErrorEvent;

            return new object[]
            {
                provider.ReceivedCount == 1,
                !string.IsNullOrEmpty(formatted),
                formatted != null && formatted.IndexOf("a request failed", StringComparison.Ordinal) >= 0,
                captured != null && captured.ErrorException == boom,
            };
        }

        // Raise a WebHeartbeatEvent and confirm WebBaseEvent.ToString(true,true) (the formatted
        // event text used by every provider) is non-empty and includes the heartbeat process
        // statistics section contributed by FormatCustomEventDetails.
        // Returns:
        //   [0] provider received the heartbeat   (bool) true
        //   [1] formatted text non-empty          (bool) true
        //   [2] formatted text has process stats   (bool) true
        //   [3] event code round-trips             (int)  ApplicationHeartbeat
        public static object[] HeartbeatEventFormatsAndDispatches()
        {
            CapturingProvider provider = new CapturingProvider();
            provider.Initialize("hb", new System.Collections.Specialized.NameValueCollection());

            TestHeartbeatEvent ev = new TestHeartbeatEvent("tick", WebEventCodes.ApplicationHeartbeat);
            provider.ProcessEvent(ev);

            string formatted = provider.LastFormatted;
            return new object[]
            {
                provider.ReceivedCount == 1,
                !string.IsNullOrEmpty(formatted),
                formatted != null && formatted.IndexOf("Process statistics", StringComparison.Ordinal) >= 0,
                ev.EventCode,
            };
        }

        // Buffering ON: ProcessEvent holds events until Flush(), at which point the whole batch
        // is delivered in a single notification.
        // Returns:
        //   [0] count before flush is zero (bool) true
        //   [1] count after flush is two   (bool) true
        public static object[] BufferingHoldsUntilFlush()
        {
            CapturingProvider provider = new CapturingProvider();
            System.Collections.Specialized.NameValueCollection config =
                new System.Collections.Specialized.NameValueCollection();
            config["buffer"] = "true";
            provider.Initialize("buffered", config);

            provider.ProcessEvent(new TestHeartbeatEvent("one", WebEventCodes.ApplicationHeartbeat));
            provider.ProcessEvent(new TestHeartbeatEvent("two", WebEventCodes.ApplicationHeartbeat));
            bool zeroBeforeFlush = provider.ReceivedCount == 0;
            provider.Flush();

            return new object[]
            {
                zeroBeforeFlush,
                provider.ReceivedCount == 2,
            };
        }
    }
}
