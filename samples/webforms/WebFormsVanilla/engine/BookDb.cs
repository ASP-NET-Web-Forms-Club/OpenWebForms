using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace WebFormsVanilla.engine
{
    // Static data-access layer for the books table, backed by SQLiteExpress.
    public static class BookDb
    {
        // Create the table (if missing) and seed the four starter rows on first use.
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
                        SeedRow(s, "The Pragmatic Programmer", "Andrew Hunt", "Non-Fiction", 1999, 1, now);
                        SeedRow(s, "Dune", "Frank Herbert", "Sci-Fi", 1965, 1, now);
                        SeedRow(s, "Clean Code", "Robert C. Martin", "Non-Fiction", 2008, 0, now);
                        SeedRow(s, "The Hobbit", "J.R.R. Tolkien", "Fantasy", 1937, 1, now);
                    }
                }
            }
        }

        static void SeedRow(SQLiteExpress s, string title, string author, string genre, int year, int inStock, string now)
        {
            s.Insert("books", new Dictionary<string, object>
            {
                ["title"] = title,
                ["author"] = author,
                ["genre"] = genre,
                ["year"] = year,
                ["in_stock"] = inStock,
                ["date_created"] = now,
            });
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
                    return s.GetObject<obBook>(
                        "SELECT * FROM books WHERE id = @vid;",
                        new Dictionary<string, object> { ["@vid"] = id });
                }
            }
        }

        public static int Insert(obBook b)
        {
            using (SQLiteConnection conn = new SQLiteConnection(Config.ConnString))
            {
                conn.Open();
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    SQLiteExpress s = new SQLiteExpress(cmd);
                    s.Insert("books", new Dictionary<string, object>
                    {
                        ["title"] = b.Title,
                        ["author"] = b.Author,
                        ["genre"] = b.Genre,
                        ["year"] = b.Year,
                        ["in_stock"] = b.InStock,
                        ["date_created"] = b.DateCreated,
                    });
                    return (int)s.LastInsertId;
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
                    s.Update("books", new Dictionary<string, object>
                    {
                        ["title"] = b.Title,
                        ["author"] = b.Author,
                        ["genre"] = b.Genre,
                        ["year"] = b.Year,
                        ["in_stock"] = b.InStock,
                    }, "id", b.Id);
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
                    s.Execute("DELETE FROM books WHERE id = @vid;",
                        new Dictionary<string, object> { ["@vid"] = id });
                }
            }
        }
    }
}
