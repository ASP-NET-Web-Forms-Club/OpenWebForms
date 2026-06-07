using System;
using System.Web;

namespace WebFormsVanilla.engine
{
    public static class Config
    {
        // Connection string to the SQLite database under ~/App_Data/library.db
        public static string ConnString
        {
            get
            {
                string dbPath = HttpContext.Current.Server.MapPath("~/App_Data/library.db");
                return "Data Source=" + dbPath + ";Version=3;";
            }
        }
    }
}
