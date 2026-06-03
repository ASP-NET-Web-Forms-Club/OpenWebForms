// The "real" entry point. This type is invoked by reflection from AlcBootstrap AFTER the host
// assembly has been loaded into the custom SystemWebLoadContext, so every System.Web type it
// references resolves to OUR app-local clean-room System.Web.dll (not the framework facade).
//
// At Tier 0, HttpRuntime.ProcessRequest throws NotImplementedException, which HttpListenerServer
// catches and turns into HTTP 500. A 500 on the smoke hit therefore PROVES our HttpRuntime ran.
// NO ASP.NET Core anywhere.
using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Web.Server;

namespace SampleHost
{
    internal static class RealEntry
    {
        public static int Run(string[] args)
        {
            bool smoke = args != null && Array.IndexOf(args, "--smoke") >= 0;

            int port = 8080;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    int parsed;
                    if (int.TryParse(args[i], out parsed))
                    {
                        port = parsed;
                        break;
                    }
                }
            }

            string baseDir = AppContext.BaseDirectory;
            string physicalDir = Path.Combine(baseDir, "wwwroot");
            if (!Directory.Exists(physicalDir))
            {
                // Fall back to the source wwwroot when running from a non-publish layout.
                physicalDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "wwwroot"));
            }

            PrintSystemWebDiagnostic();

            HttpListenerServer server = new HttpListenerServer(physicalDir, "/", port);

            using (ManualResetEventSlim stop = new ManualResetEventSlim(false))
            {
                Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
                {
                    e.Cancel = true;
                    stop.Set();
                };

                server.Start();
                Console.WriteLine("System.Web standalone host listening at " + server.Prefix);
                Console.WriteLine("Serving physical directory: " + physicalDir);
                Console.WriteLine("Tier 6: *.aspx requests are parsed, code-generated, Roslyn-compiled and run. Press Ctrl+C to stop.");

                if (smoke)
                {
                    // Drive a real .aspx end-to-end: GET default.aspx (expect HTTP 200 with a
                    // rendered control tree incl __VIEWSTATE), then POST a replayed __VIEWSTATE +
                    // __EVENTTARGET to fire the Button's server-side Click handler (true postback).
                    int rc = AspxSmoke(server.Prefix);
                    Console.WriteLine("Smoke run complete; stopping.");
                    server.Stop();
                    Console.WriteLine("Stopped.");
                    return rc;
                }
                else
                {
                    stop.Wait();
                }

                server.Stop();
                Console.WriteLine("Stopped.");
            }

            return 0;
        }

        // Proves which System.Web is actually loaded: print Location and AssemblyVersion.
        private static void PrintSystemWebDiagnostic()
        {
            Assembly sw = typeof(HttpListenerServer).Assembly;
            AssemblyLoadContext ctx = AssemblyLoadContext.GetLoadContext(sw);
            string ctxName = ctx == null ? "<unknown>" : ctx.Name;
            string location = string.IsNullOrEmpty(sw.Location) ? "<no on-disk location>" : sw.Location;
            Console.WriteLine("---- System.Web load diagnostic ----");
            Console.WriteLine("  Assembly:        " + sw.GetName().Name);
            Console.WriteLine("  AssemblyVersion: " + sw.GetName().Version);
            Console.WriteLine("  Location:        " + location);
            Console.WriteLine("  LoadContext:     " + ctxName);
            Console.WriteLine("------------------------------------");
        }

        // Drives a full GET -> POST round trip against /default.aspx and prints proof.
        // Returns 0 only if both the GET rendered a 200 page with __VIEWSTATE inside a <form>
        // AND the POST fired the server-side Click handler (Label text changed).
        private static int AspxSmoke(string prefix)
        {
            try
            {
                string url = prefix + "default.aspx";

                // ----- GET -----
                string getBody;
                int getStatus = HttpGet(url, out getBody);
                Console.WriteLine("GET  /default.aspx -> HTTP " + getStatus);
                bool hasForm = getBody.IndexOf("<form", StringComparison.OrdinalIgnoreCase) >= 0;
                bool hasViewState = getBody.IndexOf("__VIEWSTATE", StringComparison.Ordinal) >= 0;
                bool hasGrid = getBody.IndexOf("Widget", StringComparison.Ordinal) >= 0;
                Console.WriteLine("  contains <form>:       " + hasForm);
                Console.WriteLine("  contains __VIEWSTATE:  " + hasViewState);
                Console.WriteLine("  GridView bound (Widget): " + hasGrid);

                string viewState = ScrapeHidden(getBody, "__VIEWSTATE");
                string eventValidation = ScrapeHidden(getBody, "__EVENTVALIDATION");
                string submitName = ScrapeControlName(getBody, "Submit");

                if (getStatus != 200 || !hasViewState)
                {
                    Console.WriteLine("GET proof FAILED.");
                    Console.WriteLine("----- BODY -----");
                    Console.WriteLine(getBody.Length > 2000 ? getBody.Substring(0, 2000) : getBody);
                    return 1;
                }

                // ----- POST (replay __VIEWSTATE + fire the Button's Click) -----
                // The Button renders onclick="__doPostBack('Submit','')", so a real browser would
                // post __EVENTTARGET=Submit. It is also a submit input, so we send its name=value
                // pair too. Either path drives the Button's IPostBackEventHandler -> Click.
                StringBuilder form = new StringBuilder();
                Append(form, "__VIEWSTATE", viewState);
                if (eventValidation != null) { Append(form, "__EVENTVALIDATION", eventValidation); }
                Append(form, "__EVENTTARGET", submitName ?? "Submit");
                Append(form, "__EVENTARGUMENT", "");
                string nameBox = ScrapeControlName(getBody, "NameBox");
                Append(form, nameBox ?? "NameBox", "hello-from-post");
                if (submitName != null) { Append(form, submitName, "Submit"); }

                string postBody;
                int postStatus = HttpPost(url, form.ToString(), out postBody);
                Console.WriteLine("POST /default.aspx -> HTTP " + postStatus);
                bool clickRan = postBody.IndexOf("You said: hello-from-post", StringComparison.Ordinal) >= 0;
                Console.WriteLine("  Click handler ran (Label changed): " + clickRan);

                if (postStatus != 200 || !clickRan)
                {
                    Console.WriteLine("POST proof FAILED.");
                    Console.WriteLine("----- BODY -----");
                    Console.WriteLine(postBody.Length > 2000 ? postBody.Substring(0, 2000) : postBody);
                    return 1;
                }

                Console.WriteLine("ASPX SMOKE PASSED: real .aspx GET 200 + true postback Click handler fired.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Aspx smoke error: " + ex.GetType().Name + ": " + ex.Message);
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        private static int HttpGet(string url, out string body)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = 30000;
            return ReadResponse(req, out body);
        }

        private static int HttpPost(string url, string formData, out string body)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.Timeout = 30000;
            req.ContentType = "application/x-www-form-urlencoded";
            byte[] data = Encoding.UTF8.GetBytes(formData);
            req.ContentLength = data.Length;
            using (Stream rs = req.GetRequestStream())
            {
                rs.Write(data, 0, data.Length);
            }
            return ReadResponse(req, out body);
        }

        private static int ReadResponse(HttpWebRequest req, out string body)
        {
            try
            {
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    body = sr.ReadToEnd();
                    return (int)resp.StatusCode;
                }
            }
            catch (WebException wex)
            {
                HttpWebResponse resp = wex.Response as HttpWebResponse;
                if (resp != null)
                {
                    using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    {
                        body = sr.ReadToEnd();
                    }
                    int code = (int)resp.StatusCode;
                    resp.Close();
                    return code;
                }
                body = "WebException: " + wex.Message;
                return -1;
            }
        }

        private static void Append(StringBuilder sb, string key, string value)
        {
            if (sb.Length > 0) { sb.Append('&'); }
            sb.Append(Uri.EscapeDataString(key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(value ?? string.Empty));
        }

        // Scrape <input ... name="X" ... value="Y" .../> -> Y (handles attribute order both ways).
        private static string ScrapeHidden(string html, string name)
        {
            string needle = "name=\"" + name + "\"";
            int idx = html.IndexOf(needle, StringComparison.Ordinal);
            if (idx < 0) { return null; }
            int valIdx = html.IndexOf("value=\"", idx, StringComparison.Ordinal);
            if (valIdx < 0) { return null; }
            valIdx += "value=\"".Length;
            int end = html.IndexOf('"', valIdx);
            if (end < 0) { return null; }
            string raw = html.Substring(valIdx, end - valIdx);
            return HtmlAttrDecode(raw);
        }

        // Find the rendered name attribute for a control whose id ends with the given suffix.
        private static string ScrapeControlName(string html, string idSuffix)
        {
            int idx = 0;
            while (true)
            {
                int nameIdx = html.IndexOf("name=\"", idx, StringComparison.Ordinal);
                if (nameIdx < 0) { return null; }
                int start = nameIdx + "name=\"".Length;
                int end = html.IndexOf('"', start);
                if (end < 0) { return null; }
                string n = html.Substring(start, end - start);
                if (n.EndsWith(idSuffix, StringComparison.Ordinal) || n == idSuffix)
                {
                    return HtmlAttrDecode(n);
                }
                idx = end + 1;
            }
        }

        private static string HtmlAttrDecode(string raw)
        {
            return raw.Replace("&quot;", "\"").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");
        }
    }
}