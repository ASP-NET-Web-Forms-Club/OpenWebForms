<%@ Application Language="C#" %>
<script runat="server">

    // =====================================================================
    //  Tiny Pageless Web Forms test app  -  ONE route, NO .aspx page.
    //
    //  Every HTTP request flows through Application_BeginRequest. A single
    //  switch routes the path to a handler that builds a complete HTML
    //  document in C# and writes it straight to the response stream, then
    //  short-circuits the pipeline with CompleteRequest() (the pageless
    //  "EndResponse" pattern). No .aspx, no master page, no ViewState,
    //  no server controls, no page lifecycle.
    //
    //  Target: OpenWebForms clean-room System.Web running on .NET 8 / Linux.
    //
    //  NOTE (OpenWebForms alpha): the canonical pageless code would call
    //  System.Web.HttpUtility.HtmlEncode(...) for output encoding. On the
    //  OpenWebForms standalone host that currently fails to COMPILE because
    //  the .NET shared framework also ships System.Web.HttpUtility.dll, which
    //  defines the SAME type System.Web.HttpUtility -> Roslyn sees the type in
    //  two referenced assemblies (CS0433, ambiguous). The runtime swallows the
    //  compile error and silently falls back to the base HttpApplication, so
    //  the page never runs. Until that is fixed we encode with a tiny local
    //  Enc() helper instead of HttpUtility. See ANALYSIS-REPORT.md.
    // =====================================================================

    void Application_Start(object sender, EventArgs e)
    {
        // One-time init would go here (connection strings, schema, etc.).
        // The hello-world test needs none.
    }

    void Application_BeginRequest(object sender, EventArgs e)
    {
        string path = (Context.Request.Path ?? "/").ToLowerInvariant().TrimEnd('/');
        if (path.Length == 0) { path = "/"; }

        switch (path)
        {
            case "/":
            case "/hello":
                RenderHello();
                return;
        }

        // Any other path falls through to the normal pipeline (-> 404).
    }

    // The single page handler: builds the whole document as a C# string.
    void RenderHello()
    {
        System.Web.HttpResponse Response = Context.Response;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("<!DOCTYPE html>\n");
        sb.Append("<html>\n");
        sb.Append("<head>\n");
        sb.Append("    <meta charset='utf-8' />\n");
        sb.Append("    <meta name='viewport' content='width=device-width, initial-scale=1.0' />\n");
        sb.Append("    <title>Hello World - Pageless Web Forms</title>\n");
        sb.Append("    <style>\n");
        sb.Append("        body { font-family: system-ui, Segoe UI, Arial, sans-serif; margin: 3rem auto; max-width: 40rem; line-height: 1.5; color: #1c2b2b; }\n");
        sb.Append("        h1 { color: #0a7d6b; } code { background: #E0F3EF; padding: .1rem .35rem; border-radius: 4px; }\n");
        sb.Append("        .meta { color: #567; font-size: .9rem; }\n");
        sb.Append("    </style>\n");
        sb.Append("</head>\n");
        sb.Append("<body>\n");
        sb.Append("    <h1>Hello World</h1>\n");
        sb.Append("    <p>This page was rendered entirely in C# from <code>Global.asax</code> using the\n");
        sb.Append("       <strong>Pageless Web Forms</strong> architecture - no <code>.aspx</code> file,\n");
        sb.Append("       no master page, no ViewState, no server controls.</p>\n");
        sb.Append("    <p>The single routing point is <code>Application_BeginRequest</code>; this handler\n");
        sb.Append("       wrote the whole document and ended the request with <code>CompleteRequest()</code>.</p>\n");
        sb.Append("    <hr />\n");
        sb.Append("    <p class='meta'>Served by OpenWebForms (clean-room <code>System.Web</code>)<br />\n");
        sb.Append("       .NET runtime: " + System.Environment.Version + "<br />\n");
        sb.Append("       OS: " + System.Runtime.InteropServices.RuntimeInformation.OSDescription + "<br />\n");
        sb.Append("       Request path: " + Enc(Context.Request.Path) + "<br />\n");
        sb.Append("       Request method: " + Enc(Context.Request.HttpMethod) + "</p>\n");
        sb.Append("</body>\n");
        sb.Append("</html>");

        Response.ContentType = "text/html; charset=utf-8";
        Response.Write(sb.ToString());

        // Pageless "EndResponse": flush what we wrote and short-circuit the
        // pipeline so no handler (page) mapping/lifecycle runs after us.
        Response.TrySkipIisCustomErrors = true;
        try { Response.Flush(); } catch { }
        CompleteRequest();
    }

    // Minimal HTML-text encoder. Stands in for System.Web.HttpUtility.HtmlEncode,
    // which is currently unusable on the OpenWebForms host (see header note).
    static string Enc(string s)
    {
        if (string.IsNullOrEmpty(s)) { return ""; }
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }

</script>
