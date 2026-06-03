using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web;
using Xunit;

namespace System.Web.Tests
{
    // Behavioral tests for the three handover features:
    //   8a. Global.asax: an application file is discovered at the app root, compiled to an
    //       HttpApplication-derived type, Application_Start runs exactly once, and the magic
    //       per-request methods (Application_BeginRequest) are bound to the pipeline events.
    //   8b. Routing: UrlRoutingModule is installed in the default module set, so routes
    //       registered (idiomatically, in Application_Start) match and remap the request to the
    //       route handler -- both a direct IRouteHandler and a MapPageRoute -> .aspx end-to-end.
    //   8c. The CompleteRequest short-circuit pattern: Flush + SuppressContent + CompleteRequest
    //       commits headers, suppresses the body, and skips the remaining pipeline stages.
    //
    // All workers run INSIDE the ALC so System.Web (HttpRuntime, HttpApplication, the routing
    // engine, the compiled global.asax/Product.aspx) binds to OUR clean-room assembly.
    public class GlobalAsaxRoutingTests
    {
        private static SystemWebUnderTest SW => SystemWebUnderTest.Instance;

        [Fact]
        public void GlobalAsax_ApplicationStart_RunsOnce_AndBeginRequest_RunsPerRequest()
        {
            // [0] req1 X-Start-Count, [1] req1 X-Req-Count,
            // [2] req2 X-Start-Count, [3] req2 X-Req-Count
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.GlobalAsaxRoutingWorker", "GlobalAsaxStartOnceAndPerRequest");
            Assert.Equal("1", (string)r[0]);   // Application_Start ran exactly once before req 1
            Assert.Equal("1", (string)r[1]);   // Application_BeginRequest ran for req 1
            Assert.Equal("1", (string)r[2]);   // ...still once on req 2 (not re-run)
            Assert.Equal("2", (string)r[3]);   // ...BeginRequest ran again for req 2
        }

        [Fact]
        public void Routing_DefaultUrlRoutingModule_RemapsRequest_WithRouteData()
        {
            // [0] status, [1] body
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.GlobalAsaxRoutingWorker", "RoutingModuleRemapsToHandler");
            Assert.Equal("", (string)r[2]);          // no pipeline error
            Assert.Equal(200, (int)r[0]);
            Assert.Equal("WID=42", (string)r[1]);   // route matched, RouteData carried id=42
        }

        [Fact]
        public void Routing_MapPageRoute_RegisteredInApplicationStart_RendersAspx()
        {
            // [0] status, [1] body for GET /products/5
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.GlobalAsaxRoutingWorker", "MapPageRouteEndToEnd");
            Assert.Equal(200, (int)r[0]);
            Assert.Contains("PID=5", (string)r[1]);  // Product.aspx ran with RouteData id=5
        }

        [Fact]
        public void CompleteRequest_Pattern_SuppressesBody_AndSkipsRemainingStages()
        {
            // [0] status, [1] body, [2] laterStageRan, [3] X-Done header, [4] TrySkipIisCustomErrors
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.GlobalAsaxRoutingWorker", "CompleteRequestShortCircuit");
            Assert.Equal(200, (int)r[0]);
            Assert.Equal("", (string)r[1]);          // SuppressContent honored: empty body
            Assert.False((bool)r[2]);                // CompleteRequest honored: later stage skipped
            Assert.Equal("1", (string)r[3]);         // committed header survived
            Assert.True((bool)r[4]);                 // TrySkipIisCustomErrors stored
        }
    }

    public static class GlobalAsaxRoutingWorker
    {
        // ---- 8a: global.asax Application_Start once + Application_BeginRequest per request ----

        private const string GlobalAsaxCountingText =
@"<%@ Application Language=""C#"" %>
<script runat=""server"">
    void Application_Start(object sender, EventArgs e)
    {
        Application[""StartCount""] = ((int?)Application[""StartCount""] ?? 0) + 1;
    }
    void Application_BeginRequest(object sender, EventArgs e)
    {
        Response.AddHeader(""X-Start-Count"", System.Convert.ToString(Application[""StartCount""]));
        int n = ((int?)Application[""ReqCount""] ?? 0) + 1;
        Application[""ReqCount""] = n;
        Response.AddHeader(""X-Req-Count"", System.Convert.ToString(n));
    }
</script>";

        public static object[] GlobalAsaxStartOnceAndPerRequest()
        {
            string dir = CreateTempAppDir();
            try
            {
                File.WriteAllText(Path.Combine(dir, "Global.asax"), GlobalAsaxCountingText);
                ResetRuntime(dir);

                CapturingWorkerRequest r1 = RunRuntimeRequest("GET", "/a");
                CapturingWorkerRequest r2 = RunRuntimeRequest("GET", "/b");

                return new object[]
                {
                    r1.GetCapturedHeader("X-Start-Count"),
                    r1.GetCapturedHeader("X-Req-Count"),
                    r2.GetCapturedHeader("X-Start-Count"),
                    r2.GetCapturedHeader("X-Req-Count"),
                };
            }
            finally
            {
                RestoreRuntime();
                SafeDeleteDir(dir);
            }
        }

        // ---- 8b: UrlRoutingModule default-installed; route remaps to a handler with RouteData ----

        private sealed class IdEchoRouteHandler : global::System.Web.Routing.IRouteHandler
        {
            private readonly string _prefix;
            public IdEchoRouteHandler(string prefix) { _prefix = prefix; }
            public global::System.Web.IHttpHandler GetHttpHandler(global::System.Web.Routing.RequestContext requestContext)
            {
                object id = requestContext.RouteData.Values["id"];
                return new IdEchoHandler(_prefix + "=" + System.Convert.ToString(id));
            }
        }

        private sealed class IdEchoHandler : global::System.Web.IHttpHandler
        {
            private readonly string _text;
            public IdEchoHandler(string text) { _text = text; }
            public bool IsReusable { get { return false; } }
            public void ProcessRequest(HttpContext context)
            {
                context.Response.StatusCode = 200;
                context.Response.Write(_text);
            }
        }

        public static object[] RoutingModuleRemapsToHandler()
        {
            global::System.Web.HttpRuntime.SetAppPathsForTest(AppContext.BaseDirectory, "/");
            global::System.Web.Routing.RouteTable.Routes.Clear();
            try
            {
                global::System.Web.Routing.RouteTable.Routes.Add(
                    new global::System.Web.Routing.Route("widgets/{id}", new IdEchoRouteHandler("WID")));

                CapturingWorkerRequest wr = new CapturingWorkerRequest("GET", "/widgets/42", "", null, null);
                HttpContext ctx = new HttpContext(wr);

                HttpApplication app = new HttpApplication();
                app.InitInternal();   // installs the default modules, including UrlRoutingModule

                HttpContext.Current = ctx;
                ((IHttpHandler)app).ProcessRequest(ctx);
                ctx.Response.Flush();

                Exception err = ctx.Error;
                return new object[] { wr.CapturedStatus, Encoding.UTF8.GetString(wr.CapturedBody), err == null ? "" : err.ToString() };
            }
            finally
            {
                global::System.Web.Routing.RouteTable.Routes.Clear();
                HttpContext.Current = null;
            }
        }

        // ---- 8a + 8b end-to-end: MapPageRoute registered in Application_Start -> .aspx ----

        private const string GlobalAsaxRoutingText =
@"<%@ Application Language=""C#"" %>
<%@ Import Namespace=""System.Web.Routing"" %>
<script runat=""server"">
    void Application_Start(object sender, EventArgs e)
    {
        RouteTable.Routes.Clear();
        RouteTable.Routes.MapPageRoute(""product"", ""products/{id}"", ""~/Product.aspx"");
    }
</script>";

        private const string ProductAspxText =
            "<%@ Page Language=\"C#\" %><% Response.Write(\"PID=\" + System.Convert.ToString(" +
            "Page.RouteData == null ? null : Page.RouteData.Values[\"id\"])); %>";

        public static object[] MapPageRouteEndToEnd()
        {
            string dir = CreateTempAppDir();
            try
            {
                File.WriteAllText(Path.Combine(dir, "Global.asax"), GlobalAsaxRoutingText);
                File.WriteAllText(Path.Combine(dir, "Product.aspx"), ProductAspxText);
                ResetRuntime(dir);
                global::System.Web.Routing.RouteTable.Routes.Clear();

                CapturingWorkerRequest wr = RunRuntimeRequest("GET", "/products/5");
                return new object[] { wr.CapturedStatus, Encoding.UTF8.GetString(wr.CapturedBody) };
            }
            finally
            {
                global::System.Web.Routing.RouteTable.Routes.Clear();
                RestoreRuntime();
                SafeDeleteDir(dir);
            }
        }

        // ---- 8c: the CompleteRequest short-circuit snippet ----

        private sealed class ShouldNotRunHandler : global::System.Web.IHttpHandler
        {
            public bool IsReusable { get { return false; } }
            public void ProcessRequest(HttpContext context)
            {
                context.Response.Write("HANDLER-RAN");
            }
        }

        public static object[] CompleteRequestShortCircuit()
        {
            CapturingWorkerRequest wr = new CapturingWorkerRequest("GET", "/x", "", null, null);
            HttpContext ctx = new HttpContext(wr);

            HttpApplication app = new HttpApplication();
            app.Init();

            bool[] laterStageRan = new bool[1];

            app.BeginRequest += (s, e) =>
            {
                HttpResponse resp = ctx.Response;
                resp.StatusCode = 200;
                resp.AddHeader("X-Done", "1");
                // The exact snippet from the handover request:
                resp.TrySkipIisCustomErrors = true;
                try { resp.Flush(); } catch { }
                resp.SuppressContent = true;
                HttpContext.Current.ApplicationInstance.CompleteRequest();
                // Anything written after must NOT reach the wire (SuppressContent).
                resp.Write("AFTER-SUPPRESSED");
            };
            // A handler that, if the pipeline did not short-circuit, would write to the body.
            app.MapRequestHandler += (s, e) => { ctx.Handler = new ShouldNotRunHandler(); };
            // A post-handler stage that must be skipped once CompleteRequest is called early.
            app.AcquireRequestState += (s, e) => { laterStageRan[0] = true; };

            ((IHttpHandler)app).ProcessRequest(ctx);
            ctx.Response.Flush();

            bool trySkip = ctx.Response.TrySkipIisCustomErrors;
            return new object[]
            {
                wr.CapturedStatus,
                Encoding.UTF8.GetString(wr.CapturedBody),
                laterStageRan[0],
                wr.GetCapturedHeader("X-Done"),
                trySkip,
            };
        }

        // ---- shared helpers ----

        private static CapturingWorkerRequest RunRuntimeRequest(string verb, string path)
        {
            CapturingWorkerRequest wr = new CapturingWorkerRequest(verb, path, "", null, null);
            global::System.Web.HttpRuntime.ProcessRequest(wr);
            return wr;
        }

        private static void ResetRuntime(string appDir)
        {
            global::System.Web.HttpRuntime.SetAppPathsForTest(appDir, "/");
            global::System.Web.HttpRuntime.ResetApplicationForTest();
            global::System.Web.Compilation.BuildManagerEngine.ResetCachesForTest();
        }

        private static void RestoreRuntime()
        {
            // Point the runtime back at the default app root and drop the per-test app/type caches
            // so later tests behave exactly as before this one ran.
            global::System.Web.HttpRuntime.SetAppPathsForTest(AppContext.BaseDirectory, "/");
            global::System.Web.HttpRuntime.ResetApplicationForTest();
            global::System.Web.Compilation.BuildManagerEngine.ResetCachesForTest();
        }

        private static string CreateTempAppDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "owf_gasax_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void SafeDeleteDir(string dir)
        {
            try { if (dir != null && Directory.Exists(dir)) { Directory.Delete(dir, true); } }
            catch { }
        }
    }
}
