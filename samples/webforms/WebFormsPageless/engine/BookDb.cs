using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace WebFormsPageless
{
    // All data access for the books table via SQLiteExpress.
    // One SQLiteConnection per operation.
    public static class BookDb
    {
        // Create the table on first use and seed if empty. Idempotent.
        public static void EnsureSchema()
        {
            using (SQLiteConnection conn = new SQLiteConnection(Config.ConnString))
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

                    int count = s.ExecuteScalar<int>("SELECT COUNT(*) FROM books;");
                    if (count == 0)
                    {
                        string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        SeedOne(s, "The Pragmatic Programmer", "Andrew Hunt", "Non-Fiction", 1999, 1, now);
                        SeedOne(s, "Dune", "Frank Herbert", "Sci-Fi", 1965, 1, now);
                        SeedOne(s, "Clean Code", "Robert C. Martin", "Non-Fiction", 2008, 0, now);
                        SeedOne(s, "The Hobbit", "J.R.R. Tolkien", "Fantasy", 1937, 1, now);
                    }
                }
            }
        }

        static void SeedOne(SQLiteExpress s, string title, string author, string genre, int year, int inStock, string now)
        {
            Dictionary<string, object> dic = new Dictionary<string, object>();
            dic["title"] = title;
            dic["author"] = author;
            dic["genre"] = genre;
            dic["year"] = year;
            dic["in_stock"] = inStock;
            dic["date_created"] = now;
            s.Insert("books", dic);
        }

        public static List<obBook> List()
        {
            using (SQLiteConnection conn = new SQLiteConnection(Config.ConnString))
            {
                conn.Open();
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    SQLiteExpress s = new SQLiteExpress(cmd);
                    return s.GetObjectList<obBook>("SELECT * FROM books ORDER BY id DESC;");
                }
            }
        }

        public static obBook Get(int id)
        {
            using (SQLiteConnection conn = new SQLiteConnection(Config.ConnString))
            {
                conn.Open();
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    SQLiteExpress s = new SQLiteExpress(cmd);
                    Dictionary<string, object> p = new Dictionary<string, object>();
                    p["@id"] = id;
                    return s.GetObject<obBook>("SELECT * FROM books WHERE id = @id;", p);
                }
            }
        }

        public static void Insert(obBook b)
        {
            using (SQLiteConnection conn = new SQLiteConnection(Config.ConnString))
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
                    dic["in_stock"] = b.InStock;
                    dic["date_created"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    s.Insert("books", dic);
                }
            }
        }

        public static void Update(obBook b)
        {
            using (SQLiteConnection conn = new SQLiteConnection(Config.ConnString))
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
                    dic["in_stock"] = b.InStock;
                    s.Update("books", dic, "id", b.Id);
                }
            }
        }

        public static void Delete(int id)
        {
            using (SQLiteConnection conn = new SQLiteConnection(Config.ConnString))
            {
                conn.Open();
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    SQLiteExpress s = new SQLiteExpress(cmd);
                    Dictionary<string, object> p = new Dictionary<string, object>();
                    p["@id"] = id;
                    s.Execute("DELETE FROM books WHERE id = @id;", p);
                }
            }
        }
    }
}
