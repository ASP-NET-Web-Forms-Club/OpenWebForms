using System;
using System.Web.Security.AntiXss;

namespace System.Web.Tests
{
    // Workers executed INSIDE the custom AssemblyLoadContext so AntiXssEncoder binds to OUR
    // clean-room System.Web. AntiXssEncoder is a safe-list (allow-list) encoder: every output
    // method emits only an explicitly-safe character set verbatim and escapes everything else
    // into the entity/escape form for the target context. The workers feed dangerous inputs
    // (script tags, attribute breakouts, CSS expression payloads) and return the encoded
    // strings across the boundary for assertion.
    internal static class AntiXssWorker
    {
        // HtmlEncode of a script-injection payload: angle brackets, quotes and the slash must
        // not survive as literal markup.
        // Returns:
        //   [0] encoded output (string)
        //   [1] no literal '<' (bool) true
        //   [2] no literal '>' (bool) true
        public static object[] HtmlEncodeDangerous()
        {
            string input = "<script>alert('xss')</script>";
            string named = AntiXssEncoder.HtmlEncode(input, true);
            string numeric = AntiXssEncoder.HtmlEncode(input, false);
            return new object[]
            {
                named,
                named.IndexOf('<') < 0 && numeric.IndexOf('<') < 0,
                named.IndexOf('>') < 0 && numeric.IndexOf('>') < 0,
            };
        }

        // UrlEncode of a payload that tries to break out of a query-string context. Reserved and
        // dangerous characters are percent-encoded; the alphanumeric safe-list passes through.
        // Returns:
        //   [0] encoded output (string)
        //   [1] no literal '<' or '"' (bool) true
        //   [2] space encoded as %20 (bool) true
        public static object[] UrlEncodeDangerous()
        {
            string input = "a b<\"&=?";
            string encoded = AntiXssEncoder.UrlEncode(input);
            return new object[]
            {
                encoded,
                encoded.IndexOf('<') < 0 && encoded.IndexOf('"') < 0,
                encoded.IndexOf("%20", StringComparison.Ordinal) >= 0,
            };
        }

        // CssEncode of a CSS expression()/url() breakout payload: only ASCII alphanumerics
        // survive, everything else becomes a backslash hex escape.
        // Returns:
        //   [0] encoded output (string)
        //   [1] no literal '(' ')' ':' '/' (bool) true
        //   [2] alphanumerics preserved (bool) true  ('expression' substring still present)
        public static object[] CssEncodeDangerous()
        {
            string input = "expression(alert(1)):url(javascript:x)";
            string encoded = AntiXssEncoder.CssEncode(input);
            bool noSpecials = encoded.IndexOf('(') < 0 && encoded.IndexOf(')') < 0
                && encoded.IndexOf(':') < 0 && encoded.IndexOf('/') < 0;
            return new object[]
            {
                encoded,
                noSpecials,
                encoded.IndexOf("expression", StringComparison.Ordinal) >= 0,
            };
        }

        // XmlAttributeEncode of an attribute-breakout payload: quotes and angle brackets are
        // entity-encoded so the value cannot terminate the attribute or open a tag.
        // Returns:
        //   [0] encoded output (string)
        //   [1] no literal '"' (bool) true
        //   [2] no literal '<' (bool) true
        public static object[] XmlAttributeEncodeDangerous()
        {
            string input = "\" onmouseover=\"alert(1)\" <b>";
            string encoded = AntiXssEncoder.XmlAttributeEncode(input);
            return new object[]
            {
                encoded,
                encoded.IndexOf('"') < 0,
                encoded.IndexOf('<') < 0,
            };
        }
    }
}
