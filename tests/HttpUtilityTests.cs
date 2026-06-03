using System;
using System.Collections.Specialized;
using Xunit;

namespace System.Web.Tests
{
    // Tier 3 behavioral tests for System.Web.HttpUtility, invoked through the ALC
    // bridge so the static calls bind to OUR clean-room System.Web rather than the
    // strong-named framework facade that only forwards HttpUtility.
    public class HttpUtilityTests
    {
        private const string HttpUtilityTypeName = "System.Web.HttpUtility";
        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        [Fact]
        public void HtmlEncode_then_HtmlDecode_RoundTrips()
        {
            string original = "<a href=\"x\">Tom & Jerry's \"quote\"</a>";
            string encoded = (string)Web.InvokeStatic(HttpUtilityTypeName, "HtmlEncode", original);
            // Markup-significant characters must have been escaped.
            Assert.DoesNotContain("<a", encoded);
            Assert.Contains("&lt;", encoded);
            Assert.Contains("&amp;", encoded);
            string decoded = (string)Web.InvokeStatic(HttpUtilityTypeName, "HtmlDecode", encoded);
            Assert.Equal(original, decoded);
        }

        [Fact]
        public void UrlEncode_then_UrlDecode_RoundTrips()
        {
            string original = "name=Alice & Bob/ +?#=value";
            string encoded = (string)Web.InvokeStatic(HttpUtilityTypeName, "UrlEncode", original);
            // Reserved characters must not survive verbatim in the encoded form.
            Assert.DoesNotContain("&", encoded);
            Assert.DoesNotContain("?", encoded);
            string decoded = (string)Web.InvokeStatic(HttpUtilityTypeName, "UrlDecode", encoded);
            Assert.Equal(original, decoded);
        }

        [Fact]
        public void ParseQueryString_GroupsDuplicateKeys()
        {
            NameValueCollection nvc = (NameValueCollection)Web.InvokeStatic(
                HttpUtilityTypeName, "ParseQueryString", "a=1&b=2&b=3");

            Assert.Equal("1", nvc["a"]);
            // Duplicate "b" keys are grouped into a single comma-joined value.
            Assert.Equal("2,3", nvc["b"]);
            string[] bValues = nvc.GetValues("b");
            Assert.NotNull(bValues);
            Assert.Equal(2, bValues.Length);
            Assert.Equal("2", bValues[0]);
            Assert.Equal("3", bValues[1]);
        }

        [Fact]
        public void JavaScriptStringEncode_WithDoubleQuotes_WrapsAndEscapes()
        {
            string input = "he said \"hi\"\n";

            // Without quotes: escapes but does not wrap.
            string noQuotes = (string)Web.InvokeStatic(
                HttpUtilityTypeName, "JavaScriptStringEncode", input, false);
            Assert.False(noQuotes.StartsWith("\""), "should not be wrapped when addDoubleQuotes=false");
            Assert.Contains("\\\"", noQuotes);  // inner quote escaped
            Assert.Contains("\\n", noQuotes);   // newline escaped
            Assert.DoesNotContain("\n", noQuotes);

            // With quotes: same escaping plus surrounding double quotes.
            string withQuotes = (string)Web.InvokeStatic(
                HttpUtilityTypeName, "JavaScriptStringEncode", input, true);
            Assert.StartsWith("\"", withQuotes);
            Assert.EndsWith("\"", withQuotes);
            // The wrapped form is the unwrapped escaping surrounded by quotes.
            Assert.Equal("\"" + noQuotes + "\"", withQuotes);
        }
    }
}
