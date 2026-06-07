using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace WebFormsServerControls
{
    public static class BookDb
    {
        // Create the table on first use and seed it if empty. Idempotent.
        public static void EnsureSchema()
        {
            using (SQLiteConnection conn = new SQLiteConnection(Config.ConnectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    SQLiteExpress s = new SQLiteExpress(cmd);

                    s.Execute(@"
CREATE TABLE IF NOT EXISTS books (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    title        TEXT    NOT NULL DEFAULT '',
    author       TEXT    NOT NULL DEFAULT '',
    genre        TEXT    NOT NULL DEFAULT '',
    year         INTEGER NOT NULL DEFAULT 0,
    format       TEXT    NOT NULL DEFAULT '',
    tags         TEXT    NOT NULL DEFAULT '',
    availability TEXT    NOT NULL DEFAULT '',
    in_stock     INTEGER NOT NULL DEFAULT 0,
    date_created TEXT    NOT NULL DEFAULT ''
);");

                    int count = s.ExecuteScalar<int>("select count(*) from books;");
                    if (count == 0)
                    {
                        Seed(s, "The Pragmatic Programmer", "Andrew Hunt", "Non-Fiction", 1999, 1);
                        Seed(s, "Dune", "Frank Herbert", "Sci-Fi", 1965, 1);
                        Seed(s, "Clean Code", "Robert C. Martin", "Non-Fiction", 2008, 0);
                        Seed(s, "The Hobbit", "J.R.R. Tolkien", "Fantasy", 1937, 1);
                    }
                }
            }
        }

        static void Seed(SQLiteExpress s, string title, string author, string genre, int year, int inStock)
        {
            Dictionary<string, object> dic = new Dictionary<string, object>();
            dic["title"] = title;
            dic["author"] = author;
            dic["genre"] = genre;
            dic["year"] = year;
            dic["format"] = "";
            dic["tags"] = "";
            dic["availability"] = "";
            dic["in_stock"] = inStock;
            dic["date_created"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            s.Insert("books", dic);
        }

        public static List<obBook> List()
        {
            using (SQLiteConnection conn = new SQLiteConnection(Config.ConnectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    SQLiteExpress s = new SQLiteExpress(cmd);
                    return s.GetObjectList<obBook>("select * from books order by id desc;");
                }
            }
        }

        public static obBook Get(int id)
        {
            using (SQLiteConnection conn = new SQLiteConnection(Config.ConnectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    SQLiteExpress s = new SQLiteExpress(cmd);
                    Dictionary<string, object> p = new Dictionary<string, object>();
                    p["@vid"] = id;
                    return s.GetObject<obBook>("select * from books where id = @vid;", p);
                }
            }
        }

        public static int Insert(obBook b)
        {
            using (SQLiteConnection conn = new SQLiteConnection(Config.ConnectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    SQLiteExpress s = new SQLiteExpress(cmd);
                    Dictionary<string, object> dic = new Dictionary<string, object>();
                    dic["title"] = b.Title;
                    dic["author"] = b.Author;
                    dic["genre"] = b.Genre;
                    dic["year"] = b.Year;
                    dic["format"] = b.Format;
                    dic["tags"] = b.Tags;
                    dic["availability"] = b.Availability;
                    dic["in_stock"] = b.InStock;
                    dic["date_created"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    s.Insert("books", dic);
                    return (int)s.LastInsertId;
                }
            }
        }

        public static void Update(obBook b)
        {
            using (SQLiteConnection conn = new SQLiteConnection(Config.ConnectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    SQLiteExpress s = new SQLiteExpress(cmd);
                    Dictionary<string, object> dic = new Dictionary<string, object>();
                    dic["title"] = b.Title;
                    dic["author"] = b.Author;
                    dic["genre"] = b.Genre;
                    dic["year"] = b.Year;
                    dic["format"] = b.Format;
                    dic["tags"] = b.Tags;
                    dic["availability"] = b.Availability;
                    dic["in_stock"] = b.InStock;
                    s.Update("books", dic, "id", b.Id);
                }
            }
        }

        public static void Delete(int id)
        {
            using (SQLiteConnection conn = new SQLiteConnection(Config.ConnectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    SQLiteExpress s = new SQLiteExpress(cmd);
                    Dictionary<string, object> p = new Dictionary<string, object>();
                    p["@vid"] = id;
                    s.Execute("delete from books where id = @vid;", p);
                }
            }
        }
    }
}
