using System;
using System.Web;

namespace WebFormsPageless
{
    public class Global : System.Web.HttpApplication
    {
        protected void Application_Start(object sender, EventArgs e)
        {
            // One-time init: resolve connection string + ensure schema/seed.
            Config.Init();
            BookDb.EnsureSchema();
        }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            // Single routing point for the entire pageless app.
            string path = Request.Path.ToLower().TrimEnd('/');

            switch (path)
            {
                case "":
                case "/":
                    RH.BookPage.HandleRequest();
                    return;
                case "/bookapi":
                    RH.BookApi.HandleRequest();
                    return;
            }
        }
    }
}
