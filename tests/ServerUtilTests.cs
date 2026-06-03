using System;
using System.IO;
using System.Web;
using Xunit;

namespace System.Web.Tests
{
    // Proves HttpServerUtility basics: HtmlEncode / UrlEncode / UrlDecode (which delegate
    // to the Tier-1 HttpEncoder and the cshttp PercentDecoder) and MapPath (which delegates
    // to the worker request). Runs inside the custom ALC.
    public class ServerUtilTests
    {
        private static SystemWebUnderTest SW => SystemWebUnderTest.Instance;

        [Fact]
        public void HtmlEncode_EscapesMarkup()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.ServerUtilWorker", "HtmlEncode");
            Assert.Equal("&lt;b&gt;Tom &amp; Jerry&lt;/b&gt;", r[0]);
        }

        [Fact]
        public void UrlEncode_And_UrlDecode_RoundTrip()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.ServerUtilWorker", "UrlEncodeDecode");
            // [0] encoded, [1] decoded-roundtrip
            string encoded = (string)r[0];
            Assert.DoesNotContain(" ", encoded); // space must be encoded (+ or %20)
            Assert.Contains("%26", encoded);      // '&' -> %26
            Assert.Equal("a b&c=d", r[1]);
        }

        [Fact]
        public void UrlDecode_DecodesPercentAndPlus()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.ServerUtilWorker", "UrlDecode");
            Assert.Equal("hello world & friends", r[0]);
        }

        [Fact]
        public void MapPath_CombinesWithAppRoot()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.ServerUtilWorker", "MapPath");
            string mapped = (string)r[0];
            string expected = Path.Combine(AppContext.BaseDirectory, "sub" + Path.DirectorySeparatorChar + "file.txt");
            Assert.Equal(expected, mapped);
        }
    }

    public static class ServerUtilWorker
    {
        private static HttpServerUtility Server()
        {
            FakeWorkerRequest wr = new FakeWorkerRequest("GET", "/", "", null, null);
            HttpContext ctx = new HttpContext(wr);
            return ctx.Server;
        }

        public static object[] HtmlEncode()
        {
            return new object[] { Server().HtmlEncode("<b>Tom & Jerry</b>") };
        }

        public static object[] UrlEncodeDecode()
        {
            HttpServerUtility s = Server();
            string encoded = s.UrlEncode("a b&c=d");
            string decoded = s.UrlDecode(encoded);
            return new object[] { encoded, decoded };
        }

        public static object[] UrlDecode()
        {
            return new object[] { Server().UrlDecode("hello+world+%26+friends") };
        }

        public static object[] MapPath()
        {
            return new object[] { Server().MapPath("/sub/file.txt") };
        }
    }
}
