using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using Xunit;

namespace System.Web.Tests
{
    // Proves HttpRequest wiring over a fake/in-memory HttpWorkerRequest: QueryString,
    // Form (urlencoded), Files (multipart), Cookies, HttpMethod and Headers. The parsing
    // is delegated by our HttpRequest to the vendored cshttp parsers, so these tests
    // exercise that integration end to end.
    //
    // Each scenario runs inside the custom ALC (via RunInAlc) so the worker request's
    // base type binds to OUR System.Web.HttpWorkerRequest, then returns primitives back
    // across the ALC boundary for assertion.
    public class RequestTests
    {
        private static SystemWebUnderTest SW => SystemWebUnderTest.Instance;

        [Fact]
        public void QueryString_IsParsed()
        {
            // [0] a, [1] b, [2] count of values for "a"
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.RequestWorker", "QueryString");
            Assert.Equal("1", r[0]);
            Assert.Equal("two words", r[1]); // %20 / + decoded
            Assert.Equal(1, r[2]);
        }

        [Fact]
        public void HttpMethod_And_Headers_AreExposed()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.RequestWorker", "MethodAndHeaders");
            Assert.Equal("POST", r[0]);
            Assert.Equal("text/plain-agent", r[1]); // User-Agent
            Assert.Equal("application/x-www-form-urlencoded", r[2]); // Content-Type header
        }

        [Fact]
        public void Form_UrlEncodedBody_IsParsed()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.RequestWorker", "Form");
            Assert.Equal("Alice", r[0]); // name
            Assert.Equal("hello world", r[1]); // message (decoded)
        }

        [Fact]
        public void Cookies_AreParsed()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.RequestWorker", "Cookies");
            Assert.Equal("abc123", r[0]); // sid
            Assert.Equal("dark", r[1]);   // theme
        }

        [Fact]
        public void Files_MultipartBody_ProducesOnePostedFile()
        {
            // [0] count, [1] field name, [2] file name, [3] content type,
            // [4] file bytes (byte[]), [5] companion form field value
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.RequestWorker", "Files");
            Assert.Equal(1, r[0]);
            Assert.Equal("upload", r[1]);
            Assert.Equal("hello.txt", r[2]);
            Assert.Equal("text/plain", r[3]);
            Assert.Equal(Encoding.ASCII.GetBytes("File contents here"), (byte[])r[4]);
            Assert.Equal("Bob", r[5]);
        }
    }

    // Executes INSIDE the custom ALC. References to System.Web.* bind to OUR assembly.
    public static class RequestWorker
    {
        private static HttpContext MakeContext(
            string verb, string uri, string query,
            Dictionary<string, string> headers, byte[] body)
        {
            FakeWorkerRequest wr = new FakeWorkerRequest(verb, uri, query, headers, body);
            return new HttpContext(wr);
        }

        public static object[] QueryString()
        {
            HttpContext ctx = MakeContext("GET", "/page", "a=1&b=two+words", null, null);
            var qs = ctx.Request.QueryString;
            return new object[] { qs["a"], qs["b"], qs.GetValues("a").Length };
        }

        public static object[] MethodAndHeaders()
        {
            Dictionary<string, string> h = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "User-Agent", "text/plain-agent" },
                { "Content-Type", "application/x-www-form-urlencoded" },
            };
            HttpContext ctx = MakeContext("POST", "/submit", "", h, Array.Empty<byte>());
            HttpRequest req = ctx.Request;
            return new object[] { req.HttpMethod, req.UserAgent, req.Headers["Content-Type"] };
        }

        public static object[] Form()
        {
            byte[] body = Encoding.ASCII.GetBytes("name=Alice&message=hello+world");
            Dictionary<string, string> h = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Content-Type", "application/x-www-form-urlencoded" },
                { "Content-Length", body.Length.ToString() },
            };
            HttpContext ctx = MakeContext("POST", "/submit", "", h, body);
            var form = ctx.Request.Form;
            return new object[] { form["name"], form["message"] };
        }

        public static object[] Cookies()
        {
            Dictionary<string, string> h = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Cookie", "sid=abc123; theme=dark" },
            };
            HttpContext ctx = MakeContext("GET", "/", "", h, null);
            HttpCookieCollection cookies = ctx.Request.Cookies;
            string sid = cookies["sid"] == null ? null : cookies["sid"].Value;
            string theme = cookies["theme"] == null ? null : cookies["theme"].Value;
            return new object[] { sid, theme };
        }

        public static object[] Files()
        {
            string boundary = "----TestBoundary123";
            StringBuilder sb = new StringBuilder();
            sb.Append("--").Append(boundary).Append("\r\n");
            sb.Append("Content-Disposition: form-data; name=\"who\"\r\n\r\n");
            sb.Append("Bob\r\n");
            sb.Append("--").Append(boundary).Append("\r\n");
            sb.Append("Content-Disposition: form-data; name=\"upload\"; filename=\"hello.txt\"\r\n");
            sb.Append("Content-Type: text/plain\r\n\r\n");
            sb.Append("File contents here\r\n");
            sb.Append("--").Append(boundary).Append("--\r\n");
            byte[] body = Encoding.ASCII.GetBytes(sb.ToString());

            Dictionary<string, string> h = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Content-Type", "multipart/form-data; boundary=" + boundary },
                { "Content-Length", body.Length.ToString() },
            };
            HttpContext ctx = MakeContext("POST", "/upload", "", h, body);
            HttpRequest req = ctx.Request;
            HttpFileCollection files = req.Files;

            int count = files.Count;
            string fieldName = count > 0 ? files.GetKey(0) : null;
            HttpPostedFile f = count > 0 ? files[0] : null;
            string fileName = f == null ? null : f.FileName;
            string contentType = f == null ? null : f.ContentType;
            byte[] bytes = null;
            if (f != null)
            {
                using (var ms = new System.IO.MemoryStream())
                {
                    f.InputStream.CopyTo(ms);
                    bytes = ms.ToArray();
                }
            }
            string companion = req.Form["who"];
            return new object[] { count, fieldName, fileName, contentType, bytes, companion };
        }
    }
}
