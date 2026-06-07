using System;
using System.Web;
using Newtonsoft.Json;

namespace WebFormsPageless
{
    // Shared response helpers for the pageless pipeline.
    public static class ApiHelper
    {
        static HttpResponse Response
        {
            get
            {
                if (HttpContext.Current == null)
                    throw new InvalidOperationException("ApiHelper called outside of an HTTP request context.");
                return HttpContext.Current.Response;
            }
        }

        public static void WriteJson(object obj)
        {
            Response.ContentType = "application/json";
            Response.Write(JsonConvert.SerializeObject(obj));
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

        // Pageless equivalent of Response.End() — flush, suppress further
        // output, and tell ASP.NET to skip to EndRequest cleanup.
        public static void EndResponse()
        {
            Response.TrySkipIisCustomErrors = true;

            try
            {
                Response.Flush();
            }
            catch { /* client already disconnected — ignore */ }

            HttpContext.Current.ApplicationInstance.CompleteRequest();
        }
    }
}
