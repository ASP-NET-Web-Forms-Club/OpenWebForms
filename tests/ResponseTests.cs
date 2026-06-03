using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using Xunit;

namespace System.Web.Tests
{
    // Proves HttpResponse emits the right status line, headers, Set-Cookie header and
    // body bytes into the worker request on flush. Runs inside the custom ALC so that
    // HttpResponse / HttpCookie / HttpWorkerRequest all bind to OUR assembly.
    public class ResponseTests
    {
        private static SystemWebUnderTest SW => SystemWebUnderTest.Instance;

        [Fact]
        public void Response_StatusHeadersCookieAndBody_AreFlushed()
        {
            // [0] status (int), [1] statusDescription, [2] Content-Type header,
            // [3] X-Custom header, [4] Set-Cookie value, [5] body string, [6] ended (bool)
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.ResponseWorker", "WriteAndFlush");

            Assert.Equal(201, r[0]);
            Assert.Equal("Created", r[1]);
            Assert.StartsWith("text/plain", (string)r[2]);
            Assert.Equal("custom-value", r[3]);
            Assert.NotNull(r[4]);
            Assert.Contains("sid=xyz789", (string)r[4]);
            Assert.Contains("path=/", (string)r[4]);
            Assert.Equal("Hello, body!", r[5]);
            Assert.Equal(true, r[6]);
        }

        [Fact]
        public void Response_ContentLength_MatchesBodyBytes()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.ResponseWorker", "ContentLength");
            // [0] Content-Length header (string), [1] actual body byte count (int)
            Assert.Equal(r[1].ToString(), (string)r[0]);
        }
    }

    public static class ResponseWorker
    {
        public static object[] WriteAndFlush()
        {
            CapturingWorkerRequest wr = new CapturingWorkerRequest("GET", "/", "", null, null);
            HttpContext ctx = new HttpContext(wr);
            HttpResponse resp = ctx.Response;

            resp.StatusCode = 201;
            resp.StatusDescription = "Created";
            resp.ContentType = "text/plain";
            resp.AppendHeader("X-Custom", "custom-value");

            HttpCookie cookie = new HttpCookie("sid", "xyz789");
            cookie.Path = "/";
            resp.Cookies.Add(cookie);

            resp.Write("Hello, body!");
            resp.End();

            string body = Encoding.UTF8.GetString(wr.CapturedBody);
            return new object[]
            {
                wr.CapturedStatus,
                wr.CapturedStatusDescription,
                wr.GetCapturedHeader("Content-Type"),
                wr.GetCapturedHeader("X-Custom"),
                wr.GetCapturedHeader("Set-Cookie"),
                body,
                wr.Ended,
            };
        }

        public static object[] ContentLength()
        {
            CapturingWorkerRequest wr = new CapturingWorkerRequest("GET", "/", "", null, null);
            HttpContext ctx = new HttpContext(wr);
            HttpResponse resp = ctx.Response;
            resp.ContentType = "text/plain";
            resp.Write("0123456789"); // 10 ASCII bytes
            resp.Flush();
            return new object[] { wr.GetCapturedHeader("Content-Length"), wr.CapturedBody.Length };
        }
    }
}
