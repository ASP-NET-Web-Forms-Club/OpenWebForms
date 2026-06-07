using System.Web;

namespace WebFormsPageless
{
    public static class Config
    {
        // Resolved once at Application_Start.
        public static string ConnString = "";

        public static void Init()
        {
            string dbPath = HttpContext.Current.Server.MapPath("~/App_Data/library.db");
            ConnString = "Data Source=" + dbPath + ";Version=3;";
        }
    }
}
