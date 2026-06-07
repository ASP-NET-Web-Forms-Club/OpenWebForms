using System.Web;
using Newtonsoft.Json;

namespace WebFormsVanilla
{
    // JSON / response helper for the API endpoints. Lives in WebFormsVanilla (NOT System).
    public static class ApiHelper
    {
        public static HttpResponse Response
        {
            get { return HttpContext.Current.Response; }
        }

        public static void EndResponse()
        {
            // So IIS skips handling custom errors
            Response.TrySkipIisCustomErrors = true;

            try
            {
                Response.Flush();
            }
            catch { /* client already disconnected — ignore */ }

            Response.SuppressContent = true;
            HttpContext.Current.ApplicationInstance.CompleteRequest();
        }

        public static void WriteJson(object obj)
        {
            // No naming conversion — preserves names exactly as declared
            Response.ContentType = "application/json";
            Response.Write(JsonConvert.SerializeObject(obj));
        }

        public static void WriteHtml(string html)
        {
            Response.ContentType = "text/html";
            Response.Write(html);
        }

        public static void WriteSuccess(string message = "Success")
        {
            WriteJson(new { success = true, message = message });
        }

        public static void WriteError(string message, int statusCode = 400)
        {
            Response.StatusCode = statusCode;
            WriteJson(new { success = false, message = message });
        }
    }
}
