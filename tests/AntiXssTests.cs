using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier 10 (System.Web.Security.AntiXss) tests. AntiXssEncoder is a safe-list encoder; these
    // scenarios feed canonical XSS payloads and assert the dangerous characters are escaped out
    // of each target context (HTML, URL, CSS, XML attribute) while the safe-list survives.
    // Run inside the custom AssemblyLoadContext so AntiXssEncoder binds to OUR System.Web.
    public class AntiXssTests
    {
        private static SystemWebUnderTest SW => SystemWebUnderTest.Instance;

        [Fact]
        public void HtmlEncode_NeutralizesScriptTag()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.AntiXssWorker", "HtmlEncodeDangerous");
            Assert.False(string.IsNullOrEmpty((string)r[0]));
            Assert.True((bool)r[1]); // no literal '<'
            Assert.True((bool)r[2]); // no literal '>'
        }

        [Fact]
        public void UrlEncode_PercentEncodesReservedCharacters()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.AntiXssWorker", "UrlEncodeDangerous");
            Assert.False(string.IsNullOrEmpty((string)r[0]));
            Assert.True((bool)r[1]); // no literal '<' or '"'
            Assert.True((bool)r[2]); // space -> %20
        }

        [Fact]
        public void CssEncode_EscapesExpressionPayload()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.AntiXssWorker", "CssEncodeDangerous");
            Assert.False(string.IsNullOrEmpty((string)r[0]));
            Assert.True((bool)r[1]); // no '(' ')' ':' '/'
            Assert.True((bool)r[2]); // alphanumerics preserved
        }

        [Fact]
        public void XmlAttributeEncode_PreventsAttributeBreakout()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.AntiXssWorker", "XmlAttributeEncodeDangerous");
            Assert.False(string.IsNullOrEmpty((string)r[0]));
            Assert.True((bool)r[1]); // no literal '"'
            Assert.True((bool)r[2]); // no literal '<'
        }
    }
}
