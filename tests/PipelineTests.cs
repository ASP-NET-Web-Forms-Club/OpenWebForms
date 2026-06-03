using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using Xunit;

namespace System.Web.Tests
{
    // End-to-end pipeline tests. Drives the request-execution engine two ways:
    //
    //  1. ProcessRequest: HttpRuntime.ProcessRequest(workerRequest) with a programmatically
    //     registered default handler that writes a 200 text/plain body. Asserts the captured
    //     response (status + body) proves the handler ran through the runtime spine.
    //
    //  2. EventOrder: subscribes to an HttpApplication's pipeline events, then runs the
    //     pipeline via the public IHttpHandler.ProcessRequest(context) entry point, and
    //     asserts the classic integrated-pipeline stages fired in the documented order with
    //     the handler executing between PreRequestHandlerExecute and PostRequestHandlerExecute.
    public class PipelineTests
    {
        private static SystemWebUnderTest SW => SystemWebUnderTest.Instance;

        [Fact]
        public void ProcessRequest_RunsDemoHandler_Returns200WithBody()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.PipelineWorker", "ProcessRequest");
            Assert.Equal(200, r[0]);                       // captured status
            Assert.StartsWith("text/plain", (string)r[1]); // content type
            Assert.Equal("Handler says hi", r[2]);         // captured body
            Assert.Equal(true, r[3]);                      // EndOfRequest called
        }

        [Fact]
        public void Pipeline_FiresStagesInOrder_WithHandlerInTheMiddle()
        {
            string[] order = (string[])SW.RunInAlc(
                "System.Web.Tests.PipelineWorker", "EventOrder");

            // The expected canonical relative ordering. We assert each pair's relative
            // position rather than exact adjacency so the test is robust to additional
            // internal stages, while still proving the documented sequence.
            AssertBefore(order, "BeginRequest", "AuthenticateRequest");
            AssertBefore(order, "AuthenticateRequest", "AuthorizeRequest");
            AssertBefore(order, "AuthorizeRequest", "ResolveRequestCache");
            AssertBefore(order, "ResolveRequestCache", "MapRequestHandler");
            AssertBefore(order, "MapRequestHandler", "PreRequestHandlerExecute");
            AssertBefore(order, "PreRequestHandlerExecute", "HANDLER");
            AssertBefore(order, "HANDLER", "PostRequestHandlerExecute");
            AssertBefore(order, "PostRequestHandlerExecute", "ReleaseRequestState");
            AssertBefore(order, "ReleaseRequestState", "UpdateRequestCache");
            AssertBefore(order, "UpdateRequestCache", "LogRequest");
            AssertBefore(order, "LogRequest", "EndRequest");
        }

        private static void AssertBefore(string[] order, string first, string second)
        {
            int i = Array.IndexOf(order, first);
            int j = Array.IndexOf(order, second);
            Assert.True(i >= 0, "stage not fired: " + first + " (order: " + string.Join(",", order) + ")");
            Assert.True(j >= 0, "stage not fired: " + second + " (order: " + string.Join(",", order) + ")");
            Assert.True(i < j, first + " should fire before " + second + " (order: " + string.Join(",", order) + ")");
        }
    }

    public static class PipelineWorker
    {
        private sealed class DemoHandler : IHttpHandler
        {
            public bool IsReusable { get { return false; } }
            public void ProcessRequest(HttpContext context)
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                context.Response.Write("Handler says hi");
            }
        }

        public static object[] ProcessRequest()
        {
            // Install a programmatic default handler via the internal hook, then run the
            // full runtime spine. The capturing worker records what reaches the wire.
            SetDefaultHandlerFactory(ctx => new DemoHandler());
            try
            {
                CapturingWorkerRequest wr = new CapturingWorkerRequest("GET", "/demo", "", null, null);
                HttpRuntime.ProcessRequest(wr);
                string body = Encoding.UTF8.GetString(wr.CapturedBody);
                return new object[] { wr.CapturedStatus, wr.GetCapturedHeader("Content-Type"), body, wr.Ended };
            }
            finally
            {
                SetDefaultHandlerFactory(null);
            }
        }

        public static string[] EventOrder()
        {
            List<string> order = new List<string>();

            CapturingWorkerRequest wr = new CapturingWorkerRequest("GET", "/", "", null, null);
            HttpContext ctx = new HttpContext(wr);

            HttpApplication app = new HttpApplication();
            app.Init();

            app.BeginRequest += (s, e) => order.Add("BeginRequest");
            app.AuthenticateRequest += (s, e) => order.Add("AuthenticateRequest");
            app.AuthorizeRequest += (s, e) => order.Add("AuthorizeRequest");
            app.ResolveRequestCache += (s, e) => order.Add("ResolveRequestCache");
            app.MapRequestHandler += (s, e) =>
            {
                order.Add("MapRequestHandler");
                // Provide the handler at the canonical mapping stage; it records itself
                // when executed so we can prove it ran between Pre/Post handler-execute.
                ctx.Handler = new RecordingHandler(order);
            };
            app.PreRequestHandlerExecute += (s, e) => order.Add("PreRequestHandlerExecute");
            app.PostRequestHandlerExecute += (s, e) => order.Add("PostRequestHandlerExecute");
            app.ReleaseRequestState += (s, e) => order.Add("ReleaseRequestState");
            app.UpdateRequestCache += (s, e) => order.Add("UpdateRequestCache");
            app.LogRequest += (s, e) => order.Add("LogRequest");
            app.EndRequest += (s, e) => order.Add("EndRequest");

            // Drive the pipeline through the public IHttpHandler entry point.
            ((IHttpHandler)app).ProcessRequest(ctx);

            return order.ToArray();
        }

        private sealed class RecordingHandler : IHttpHandler
        {
            private readonly List<string> _order;
            public RecordingHandler(List<string> order) { _order = order; }
            public bool IsReusable { get { return false; } }
            public void ProcessRequest(HttpContext context)
            {
                _order.Add("HANDLER");
                context.Response.StatusCode = 200;
                context.Response.Write("ok");
            }
        }

        // Bridges to HttpApplication's internal static SetDefaultHandlerFactory via reflection,
        // since the hook is internal to System.Web. Executes inside the ALC so the bound types
        // are OURS.
        private static void SetDefaultHandlerFactory(Func<HttpContext, IHttpHandler> factory)
        {
            System.Reflection.MethodInfo mi = typeof(HttpApplication).GetMethod(
                "SetDefaultHandlerFactory",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (mi == null)
            {
                throw new MissingMethodException("HttpApplication.SetDefaultHandlerFactory");
            }
            mi.Invoke(null, new object[] { factory });
        }
    }
}
