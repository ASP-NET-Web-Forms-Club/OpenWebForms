using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier-6 gate: a REAL .aspx file is parsed, code-generated, Roslyn-compiled, loaded into the
    // System.Web ALC, instantiated, and run end-to-end. Asserts the generated source builds the
    // control tree, the page derives from our Page, literal text + server controls render, a
    // settable property maps to a CLR property, an unknown tag attribute falls through to
    // IAttributeAccessor, and AutoEventWireup hooked Page_Load (which mutated a Label).
    public class CompilationTests
    {
        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        [Fact]
        public void CompilesAndRunsRealAspx()
        {
            object[] r = (object[])Web.RunInAlc(
                "System.Web.Tests.CompilationWorker", "CompileAndRun");

            string generated = (string)r[0];
            string typeName = (string)r[1];
            bool derives = (bool)r[2];
            string body = (string)r[3];

            // Codegen produced a build method and the expected class.
            Assert.False(string.IsNullOrEmpty(generated), "no generated source captured");
            Assert.Contains("__BuildControlTree", generated);
            Assert.Contains("FrameworkInitialize", generated);

            // Compiled into ASP.<class> deriving from our Page.
            Assert.StartsWith("ASP.", typeName);
            Assert.True(derives, "compiled page does not derive from System.Web.UI.Page");

            // Static literal markup rendered.
            Assert.Contains("static-literal", body);

            // AutoEventWireup hooked Page_Load, which set the Label's Text.
            Assert.Contains("Hello from codegen", body);

            // The second Label rendered its tagged (unknown) attribute via IAttributeAccessor and
            // its Text via the CLR property.
            Assert.Contains("tagged", body);
            Assert.Contains("data-x=\"custom\"", body);
        }

        // Regression gate for the live-host empty-body bug: drive a real .aspx through the SAME path
        // the standalone host uses -- HttpRuntime.ProcessRequest(workerRequest) -> PageHandlerFactory
        // -> BuildManager -> Page.ProcessRequest -> render -> Response.Flush -- against a worker that
        // HONORS the first Content-Length header (like System.Net.HttpListener). Before the fix the
        // response Output StreamWriter used AutoFlush + a BOM-emitting UTF-8 encoding, so an early
        // flush locked Content-Length at 3 (the BOM) and the body was truncated to nothing. This
        // asserts the full rendered markup + __VIEWSTATE survive and that the declared Content-Length
        // matches the bytes actually delivered (no premature/short Content-Length, no BOM).
        [Fact]
        public void RendersFullPageThroughRuntimeAndHonorsContentLength()
        {
            object[] r = (object[])Web.RunInAlc(
                "System.Web.Tests.CompilationWorker", "RenderThroughRuntime");

            string body = (string)r[0];
            int status = (int)r[1];
            long declaredContentLength = (long)r[2];
            long capturedByteCount = (long)r[3];

            Assert.Equal(200, status);

            // The page actually rendered, not an empty/BOM-only body.
            Assert.True(body.Length > 100, "rendered body is suspiciously small: '" + body + "'");
            // No stray UTF-8 BOM (U+FEFF) leaked into the body. The pre-fix Output StreamWriter used a
            // BOM-emitting UTF-8 encoding, so the body began with a byte-order mark.
            Assert.True(body.IndexOf('﻿') < 0, "a UTF-8 BOM leaked into the rendered body");

            // Static literal + the control tree + the AutoEventWireup'd Page_Load mutation all rendered.
            Assert.Contains("static-literal", body);
            Assert.Contains("runtime-rendered", body);          // Label text set in Page_Load
            Assert.Contains("<form", body);
            Assert.Contains("name=\"Box\"", body);              // TextBox rendered its input
            Assert.Contains("__VIEWSTATE", body);               // hidden viewstate field present
            // __VIEWSTATE must be inside the server form, not floating before it.
            int formIdx = body.IndexOf("<form", StringComparison.OrdinalIgnoreCase);
            int vsIdx = body.IndexOf("__VIEWSTATE", StringComparison.Ordinal);
            int formEndIdx = body.IndexOf("</form>", StringComparison.OrdinalIgnoreCase);
            Assert.True(formIdx >= 0 && vsIdx > formIdx && vsIdx < formEndIdx,
                "__VIEWSTATE must be rendered inside the <form>");

            // The worker received a Content-Length and it matched the bytes actually delivered: the
            // response was buffered and flushed once with a correct length (the heart of the fix).
            Assert.True(declaredContentLength >= 0, "no Content-Length header was sent");
            Assert.Equal(declaredContentLength, capturedByteCount);
        }

        // Compile-time/runtime master-page merge: a real .master + a content .aspx (MasterPageFile +
        // <asp:Content ContentPlaceHolderID="Main">) are compiled and the content page is rendered.
        // The master's header/footer literals must surround the page's content (injected into the
        // matching <asp:ContentPlaceHolder>), the placeholder's default content must be replaced, the
        // content page's <asp:Label> must render inside the placeholder, and Page.Master is non-null.
        [Fact]
        public void MergesMasterPageWithContentRegions()
        {
            object[] r = (object[])Web.RunInAlc(
                "System.Web.Tests.CompilationWorker", "MasterContentMergeThroughPipeline");

            string body = (string)r[0];
            bool masterNonNull = (bool)r[1];
            string generated = (string)r[2];

            Assert.True(masterNonNull, "Page.Master was not resolved");

            // Generated content page registers content templates and sets the master file.
            Assert.Contains("AddContentTemplate", generated);
            Assert.Contains("MasterPageFile", generated);

            // Master chrome surrounds the page content.
            Assert.Contains("[MASTER-HEADER]", body);
            Assert.Contains("[MASTER-FOOTER]", body);
            // Page content injected into the placeholder.
            Assert.Contains("[CONTENT-FROM-PAGE]", body);
            Assert.Contains("label-in-content", body);
            // Placeholder default content was replaced by the page's content.
            Assert.DoesNotContain("default-placeholder", body);

            // Ordering: header before content before footer.
            int h = body.IndexOf("[MASTER-HEADER]", StringComparison.Ordinal);
            int c = body.IndexOf("[CONTENT-FROM-PAGE]", StringComparison.Ordinal);
            int f = body.IndexOf("[MASTER-FOOTER]", StringComparison.Ordinal);
            Assert.True(h >= 0 && c > h && f > c, "master/content/footer order is wrong");
        }
    }
}
