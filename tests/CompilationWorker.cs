using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web;
using System.Web.Compilation;
using System.Web.UI;

namespace System.Web.Tests
{
    // Worker executed INSIDE the custom AssemblyLoadContext (via RunInAlc) so that Page/Control and
    // the BuildManager bind to OUR clean-room System.Web, and the assembly the BuildManager compiles
    // at runtime loads into the SAME ALC.
    //
    // It writes a real .aspx file next to the test assembly (AppContext.BaseDirectory is the app
    // root the BuildManager maps virtual paths against), then drives the full
    // parse -> codegen -> Roslyn-compile -> load -> run pipeline and renders the page.
    internal static class CompilationWorker
    {
        // Returns object[]:
        //   [0] generatedSource (string)  -- the emitted C# (via GetCompiledCustomString)
        //   [1] typeName (string)         -- compiled page type full name
        //   [2] derivesFromPage (bool)
        //   [3] renderedBody (string)     -- full GET response body
        public static object[] CompileAndRun()
        {
            string baseDir = AppContext.BaseDirectory;
            string fileName = "buildmanager_smoke.aspx";
            string physical = Path.Combine(baseDir, fileName);

            string aspx =
                "<%@ Page Language=\"C#\" AutoEventWireup=\"true\" %>\r\n" +
                "<script runat=\"server\">\r\n" +
                "  protected void Page_Load(object sender, System.EventArgs e) {\r\n" +
                "    Greeting.Text = \"Hello from codegen\";\r\n" +
                "  }\r\n" +
                "</script>\r\n" +
                "<html><body>\r\n" +
                "<span>static-literal</span>\r\n" +
                "<asp:Label runat=\"server\" ID=\"Greeting\" Text=\"initial\" />\r\n" +
                "<asp:Label runat=\"server\" ID=\"Tagged\" data-x=\"custom\" Text=\"tagged\" />\r\n" +
                "</body></html>\r\n";

            File.WriteAllText(physical, aspx, Encoding.UTF8);

            try
            {
                string virtualPath = "/" + fileName;

                Type pageType = BuildManager.GetCompiledType(virtualPath);
                string generated = BuildManager.GetCompiledCustomString(virtualPath);

                bool derives = typeof(Page).IsAssignableFrom(pageType);

                // Run a GET through the compiled page and capture its HTML.
                Page page = (Page)Activator.CreateInstance(pageType);
                CapturingWorkerRequest wr = new CapturingWorkerRequest("GET", virtualPath, "", null, Array.Empty<byte>());
                HttpContext ctx = new HttpContext(wr);
                page.ProcessRequest(ctx);
                ctx.Response.Flush();
                string body = Encoding.UTF8.GetString(wr.CapturedBody);

                return new object[]
                {
                    generated,
                    pageType.FullName,
                    derives,
                    body,
                };
            }
            finally
            {
                try { File.Delete(physical); } catch (Exception) { }
            }
        }

        // Drives a real .aspx through the SAME path the live host uses:
        // HttpRuntime.ProcessRequest(workerRequest) -> pipeline -> MapHandler (PageHandlerFactory ->
        // BuildManager) -> Page.ProcessRequest -> render -> Response.Flush. The worker request here
        // HONORS the first Content-Length header it is sent (like System.Net.HttpListener does), so a
        // premature/too-small Content-Length truncates the captured body. This is the regression that
        // produced an empty (BOM-only) live page; rendering the page directly (CompileAndRun) cannot
        // catch it because that path never exercises the runtime's flush/Content-Length sequencing.
        //
        // Returns object[]:
        //   [0] body (string)          -- decoded captured response body
        //   [1] status (int)
        //   [2] declaredContentLength (long)  -- the Content-Length header value sent (-1 if none)
        //   [3] capturedByteCount (long)      -- bytes actually accepted by the honoring worker
        public static object[] RenderThroughRuntime()
        {
            string baseDir = AppContext.BaseDirectory;
            string fileName = "runtime_render_smoke.aspx";
            string physical = Path.Combine(baseDir, fileName);

            string aspx =
                "<%@ Page Language=\"C#\" AutoEventWireup=\"true\" %>\r\n" +
                "<script runat=\"server\">\r\n" +
                "  protected void Page_Load(object sender, System.EventArgs e) {\r\n" +
                "    Greeting.Text = \"runtime-rendered\";\r\n" +
                "  }\r\n" +
                "</script>\r\n" +
                "<html><body>\r\n" +
                "<span>static-literal</span>\r\n" +
                "<form id=\"f\" runat=\"server\">\r\n" +
                "<asp:Label runat=\"server\" ID=\"Greeting\" Text=\"initial\" />\r\n" +
                "<asp:TextBox runat=\"server\" ID=\"Box\" />\r\n" +
                "</form>\r\n" +
                "</body></html>\r\n";

            // Write without a UTF-8 BOM, matching a real .aspx on disk (the host's default.aspx has
            // none). The page body must come out BOM-free regardless, but this keeps the fixture
            // realistic so the test exercises the same input shape as the live host.
            File.WriteAllText(physical, aspx, new UTF8Encoding(false));
            try
            {
                ContentLengthHonoringWorkerRequest wr =
                    new ContentLengthHonoringWorkerRequest("GET", "/" + fileName, "", null, Array.Empty<byte>());
                HttpRuntime.ProcessRequest(wr);
                string body = Encoding.UTF8.GetString(wr.CapturedBody);
                return new object[]
                {
                    body,
                    wr.CapturedStatus,
                    wr.DeclaredContentLength,
                    (long)wr.CapturedBody.Length,
                };
            }
            finally
            {
                try { File.Delete(physical); } catch (Exception) { }
            }
        }

        // Compiles a real .master + a content .aspx that targets it, then renders the content page
        // through the full parse -> codegen -> compile -> load -> run pipeline. Verifies the runtime
        // master merge: the master's header/footer literals surround the content page's <asp:Content>
        // markup (injected into the matching <asp:ContentPlaceHolder>), and Page.Master is non-null.
        //
        // Returns object[]:
        //   [0] body (string)             -- rendered content-page response
        //   [1] masterIsNonNull (bool)    -- Page.Master resolved
        //   [2] generated (string)        -- generated source for the content page
        public static object[] MasterContentMergeThroughPipeline()
        {
            string baseDir = AppContext.BaseDirectory;
            string masterName = "merge_layout.master";
            string pageName = "merge_content.aspx";
            string masterPhysical = Path.Combine(baseDir, masterName);
            string pagePhysical = Path.Combine(baseDir, pageName);

            string master =
                "<%@ Master Language=\"C#\" %>\r\n" +
                "<html><body>\r\n" +
                "[MASTER-HEADER]\r\n" +
                "<asp:ContentPlaceHolder runat=\"server\" ID=\"Main\">default-placeholder</asp:ContentPlaceHolder>\r\n" +
                "[MASTER-FOOTER]\r\n" +
                "</body></html>\r\n";

            string page =
                "<%@ Page Language=\"C#\" MasterPageFile=\"~/" + masterName + "\" %>\r\n" +
                "<asp:Content runat=\"server\" ContentPlaceHolderID=\"Main\">\r\n" +
                "[CONTENT-FROM-PAGE]\r\n" +
                "<asp:Label runat=\"server\" ID=\"Lbl\" Text=\"label-in-content\" />\r\n" +
                "</asp:Content>\r\n";

            File.WriteAllText(masterPhysical, master, new UTF8Encoding(false));
            File.WriteAllText(pagePhysical, page, new UTF8Encoding(false));
            try
            {
                string virtualPath = "/" + pageName;
                string generated = BuildManager.GetCompiledCustomString(virtualPath);
                Type pageType = BuildManager.GetCompiledType(virtualPath);

                Page p = (Page)Activator.CreateInstance(pageType);
                CapturingWorkerRequest wr = new CapturingWorkerRequest("GET", virtualPath, "", null, Array.Empty<byte>());
                HttpContext ctx = new HttpContext(wr);
                p.ProcessRequest(ctx);
                ctx.Response.Flush();
                string body = Encoding.UTF8.GetString(wr.CapturedBody);
                bool masterNonNull = p.Master != null;

                return new object[] { body, masterNonNull, generated };
            }
            finally
            {
                try { File.Delete(masterPhysical); } catch (Exception) { }
                try { File.Delete(pagePhysical); } catch (Exception) { }
            }
        }
    }
}
