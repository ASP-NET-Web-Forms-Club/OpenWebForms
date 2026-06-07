using System;
using System.Web;

namespace WebFormsServerControls
{
    public static class Config
    {
        // SQLite connection string. The database file lives under ~/App_Data/library.db.
        public static string ConnectionString
        {
            get
            {
                string dbPath = HttpContext.Current.Server.MapPath("~/App_Data/library.db");
                return "Data Source=" + dbPath + ";Version=3;";
            }
        }
    }
}
