using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using Xunit;

namespace System.Web.Tests
{
    // Tier 3 behavioral tests for System.Web.SessionState.
    //
    //  1. HttpSessionState surface: add/get/remove/count/SessionID/Timeout on a
    //     container-backed session (constructed directly).
    //  2. A two-request InProc round-trip driven through the pipeline with the
    //     SessionStateModule wired up: request 1 creates a session, stores an item,
    //     and emits the ASP.NET_SessionId cookie; request 2 carries that cookie and
    //     observes the same item from the process-wide InProc store.
    //
    // Both run INSIDE the ALC so HttpSessionStateContainer, SessionStateModule and
    // HttpContext all bind to OUR clean-room System.Web.
    public class SessionStateTests
    {
        private static SystemWebUnderTest SW => SystemWebUnderTest.Instance;

        [Fact]
        public void HttpSessionState_SetGetRemoveCount_Works()
        {
            // [0] count-after-adds, [1] value of "name", [2] count-after-remove,
            // [3] sessionId, [4] timeout
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.SessionStateWorker", "ContainerSurface");
            Assert.Equal(2, r[0]);
            Assert.Equal("Alice", r[1]);
            Assert.Equal(1, r[2]);
            Assert.Equal("abc123def456ghi789jkl012", r[3]); // the id we constructed with
            Assert.Equal(20, r[4]);
        }

        [Fact]
        public void InProc_TwoRequest_RoundTrip_PersistsItemAcrossRequests()
        {
            // [0] sessionId from request 1 cookie (non-empty), [1] item observed on
            // request 2, [2] request-2 IsNewSession is false
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.SessionStateWorker", "TwoRequestRoundTrip");
            string sid = (string)r[0];
            Assert.False(string.IsNullOrEmpty(sid), "request 1 should emit a session id cookie");
            Assert.Equal("hello-from-request-1", r[1]);
            Assert.Equal(false, r[2]);
        }
    }

    public static class SessionStateWorker
    {
        public static object[] ContainerSurface()
        {
            global::System.Web.SessionState.HttpSessionStateContainer container =
                new global::System.Web.SessionState.HttpSessionStateContainer(
                    "abc123def456ghi789jkl012",
                    new global::System.Web.SessionState.SessionStateItemCollection(),
                    new global::System.Web.HttpStaticObjectsCollection(),
                    20,
                    true,
                    HttpCookieMode.UseCookies,
                    global::System.Web.SessionState.SessionStateMode.InProc,
                    false);

            global::System.Web.SessionState.HttpSessionState session =
                new global::System.Web.SessionState.HttpSessionState(container);

            session.Add("name", "Alice");
            session.Add("count", 42);
            int afterAdds = session.Count;
            string name = (string)session["name"];

            session.Remove("count");
            int afterRemove = session.Count;

            return new object[] { afterAdds, name, afterRemove, session.SessionID, session.Timeout };
        }

        // Handler that, depending on a flag, either writes a session item (request 1)
        // or reads it back (request 2). The read result is captured via static slots.
        private sealed class SessionHandler : IHttpHandler,
            global::System.Web.SessionState.IRequiresSessionState
        {
            private readonly bool _write;
            public SessionHandler(bool write) { _write = write; }
            public bool IsReusable { get { return false; } }
            public void ProcessRequest(HttpContext context)
            {
                if (_write)
                {
                    context.Session["greeting"] = "hello-from-request-1";
                }
                else
                {
                    s_observedValue = context.Session["greeting"] as string;
                    s_request2IsNew = context.Session.IsNewSession;
                }
                context.Response.StatusCode = 200;
                context.Response.Write("ok");
            }
        }

        private static string s_observedValue;
        private static bool s_request2IsNew;

        public static object[] TwoRequestRoundTrip()
        {
            s_observedValue = null;
            s_request2IsNew = true;

            // ---- Request 1: create session, store item, emit cookie ----
            string sessionCookie = RunOnePipelineRequest(true, null);
            string sid = ExtractSessionId(sessionCookie);

            // ---- Request 2: carry the cookie, read the stored item ----
            string cookieHeader = "ASP.NET_SessionId=" + sid;
            RunOnePipelineRequest(false, cookieHeader);

            return new object[] { sid, s_observedValue, s_request2IsNew };
        }

        // Drives a single request through an HttpApplication with the SessionStateModule
        // wired up. Returns the raw Set-Cookie header carrying the session id (or null).
        private static string RunOnePipelineRequest(bool write, string cookieHeader)
        {
            Dictionary<string, string> headers = null;
            if (!string.IsNullOrEmpty(cookieHeader))
            {
                headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Cookie", cookieHeader },
                };
            }

            CapturingWorkerRequest wr = new CapturingWorkerRequest("GET", "/s", "", headers, null);
            HttpContext ctx = new HttpContext(wr);

            HttpApplication app = new HttpApplication();
            app.Init();

            // Wire the session module so it runs on AcquireRequestState / Release / EndRequest.
            global::System.Web.SessionState.SessionStateModule module =
                new global::System.Web.SessionState.SessionStateModule();
            module.Init(app);

            // Provide the handler at the canonical mapping stage.
            app.MapRequestHandler += (s, e) => { ctx.Handler = new SessionHandler(write); };

            ((IHttpHandler)app).ProcessRequest(ctx);

            // The pipeline itself does not push the response to the worker; flush so
            // status, headers and Set-Cookie (incl. the session id cookie) reach the wire.
            ctx.Response.Flush();

            List<string> setCookies = wr.GetCapturedHeaders("Set-Cookie");
            for (int i = 0; i < setCookies.Count; i++)
            {
                if (setCookies[i] != null &&
                    setCookies[i].IndexOf("ASP.NET_SessionId", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return setCookies[i];
                }
            }
            return null;
        }

        // Pulls the session id out of a "ASP.NET_SessionId=<id>; path=/; ..." Set-Cookie value.
        private static string ExtractSessionId(string setCookieValue)
        {
            if (string.IsNullOrEmpty(setCookieValue)) { return null; }
            int eq = setCookieValue.IndexOf('=');
            if (eq < 0) { return null; }
            string rest = setCookieValue.Substring(eq + 1);
            int semi = rest.IndexOf(';');
            if (semi >= 0) { rest = rest.Substring(0, semi); }
            return rest.Trim();
        }
    }
}
