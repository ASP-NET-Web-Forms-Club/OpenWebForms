using System;
using System.IO;
using System.Reflection;
using System.Text;
using Xunit;

namespace System.Web.Tests
{
    // Tier 1 behavioral tests for System.Web.Util.HttpEncoder.
    //
    // HttpEncoder's encode/decode methods are protected internal virtual and
    // write into a TextWriter (or return a string). We obtain the Default
    // instance via the public static Default property and invoke the
    // protected members reflectively through the ALC bridge so the calls bind
    // to OUR System.Web, not the framework facade.
    public class UtilTests
    {
        private const string EncoderTypeName = "System.Web.Util.HttpEncoder";
        private const BindingFlags ProtInstance =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        private static object DefaultEncoder()
        {
            object enc = Web.GetStaticProperty(EncoderTypeName, "Default");
            Assert.NotNull(enc);
            return enc;
        }

        // Invoke a (string value, TextWriter output) protected method and return what was written.
        private static string InvokeWriter(object encoder, string method, string value)
        {
            Type encType = encoder.GetType();
            MethodInfo mi = encType.GetMethod(
                method, ProtInstance, null,
                new Type[] { typeof(string), typeof(TextWriter) }, null);
            Assert.True(mi != null, "Could not find " + method + "(string, TextWriter)");
            using (StringWriter sw = new StringWriter())
            {
                mi.Invoke(encoder, new object[] { value, sw });
                return sw.ToString();
            }
        }

        [Fact]
        public void HtmlEncode_EncodesAmpLtGtQuote()
        {
            object enc = DefaultEncoder();
            // Input contains < a > & "  -> standard HTML text encoding.
            string outp = InvokeWriter(enc, "HtmlEncode", "<a>&\"");
            Assert.Equal("&lt;a&gt;&amp;&quot;", outp);
        }

        [Fact]
        public void HtmlEncode_then_HtmlDecode_RoundTrips()
        {
            object enc = DefaultEncoder();
            string original = "<a href=\"x\">Tom & Jerry</a>";
            string encoded = InvokeWriter(enc, "HtmlEncode", original);
            string decoded = InvokeWriter(enc, "HtmlDecode", encoded);
            Assert.Equal(original, decoded);
        }

        [Fact]
        public void HtmlDecode_DecodesNamedAndNumericEntities()
        {
            object enc = DefaultEncoder();
            // &lt; &gt; &amp; &quot; named, plus &#65; (A) and &#x42; (B) numeric.
            string decoded = InvokeWriter(enc, "HtmlDecode", "&lt;a&gt;&amp;&quot;&#65;&#x42;");
            Assert.Equal("<a>&\"AB", decoded);
        }

        [Fact]
        public void UrlEncode_KnownByteSequence_ProducesPercentAndPlus()
        {
            object enc = DefaultEncoder();
            // "a b&c" -> 'a' safe, ' ' -> '+', 'b' safe, '&' -> %26, 'c' safe.
            byte[] input = Encoding.ASCII.GetBytes("a b&c");
            Type encType = enc.GetType();
            MethodInfo mi = encType.GetMethod(
                "UrlEncode", ProtInstance, null,
                new Type[] { typeof(byte[]), typeof(int), typeof(int) }, null);
            Assert.True(mi != null, "Could not find UrlEncode(byte[], int, int)");
            byte[] result = (byte[])mi.Invoke(enc, new object[] { input, 0, input.Length });
            string asText = Encoding.ASCII.GetString(result);
            Assert.Equal("a+b%26c", asText);
        }

        [Fact]
        public void JavaScriptStringEncode_EscapesQuotesNewlinesAndControls()
        {
            object enc = DefaultEncoder();
            Type encType = enc.GetType();
            MethodInfo mi = encType.GetMethod(
                "JavaScriptStringEncode", ProtInstance, null,
                new Type[] { typeof(string) }, null);
            Assert.True(mi != null, "Could not find JavaScriptStringEncode(string)");
            // Build input: a " b <LF> <CR> <TAB> \ <0x01>
            string input = "a\"b\n\r\t\\" + ((char)1).ToString();
            string outp = (string)mi.Invoke(enc, new object[] { input });
            // " => \"  ;  LF => \n  ;  CR => \r  ;  TAB => \t  ;  \ => \\  ;  0x01 => uXXXX
            string expected = "a\\\"b\\n\\r\\t\\\\\\u0001";
            Assert.Equal(expected, outp);
        }
    }
}