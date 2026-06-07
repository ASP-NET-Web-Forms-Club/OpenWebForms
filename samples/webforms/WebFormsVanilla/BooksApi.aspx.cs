using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using WebFormsVanilla.engine;

namespace WebFormsVanilla
{
    public partial class BooksApi : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            try
            {
                BookDb.EnsureSchema();

                // Route by action — works for both GET querystring and POST FormData.
                string action = (Request["action"] + "").ToLower().Trim();

                switch (action)
                {
                    case "list": ListBooks(); break;
                    case "get": GetBook(); break;
                    case "save": SaveBook(); break;
                    case "delete": DeleteBook(); break;
                    default: ApiHelper.WriteError("Unknown action: " + action, 400); break;
                }
            }
            catch (Exception ex)
            {
                ApiHelper.WriteError(ex.Message, 500);
            }

            ApiHelper.EndResponse();
        }

        // Returns a server-rendered HTML fragment (table.grid) of all books.
        void ListBooks()
        {
            List<obBook> books = BookDb.List();

            StringBuilder sb = new StringBuilder();

            if (books.Count == 0)
            {
                ApiHelper.WriteHtml("<div class='empty'>No books yet. Add one on the left.</div>");
                return;
            }

            sb.Append(@"
<table class='grid'>
    <thead>
        <tr>
            <th>Title</th><th>Author</th><th>Genre</th><th>Year</th><th>In Stock</th><th>Actions</th>
        </tr>
    </thead>
    <tbody>");

            for (int i = 0; i < books.Count; i++)
            {
                obBook b = books[i];

                string title = HttpUtility.HtmlEncode(b.Title);
                string author = HttpUtility.HtmlEncode(b.Author);
                string genre = HttpUtility.HtmlEncode(b.Genre);

                string badge = b.InStock == 1
                    ? "<span class='badge badge-yes'>Yes</span>"
                    : "<span class='badge badge-no'>No</span>";

                sb.Append($@"
        <tr>
            <td>{title}</td>
            <td>{author}</td>
            <td>{genre}</td>
            <td>{b.Year}</td>
            <td>{badge}</td>
            <td class='actions'>
                <button type='button' class='link-action' onclick='editBook({b.Id})'>Edit</button>
                <button type='button' class='link-action link-danger' onclick='deleteBook({b.Id})'>Delete</button>
            </td>
        </tr>");
            }

            sb.Append(@"
    </tbody>
</table>");

            ApiHelper.WriteHtml(sb.ToString());
        }

        // Returns JSON of one book.
        void GetBook()
        {
            int id = ParseInt(Request["id"]);
            obBook b = BookDb.Get(id);

            if (b == null || b.Id == 0)
            {
                ApiHelper.WriteError("Book not found.", 404);
                return;
            }

            ApiHelper.WriteJson(new { success = true, item = b });
        }

        // Insert (id empty/0) or update (id > 0). Validates per SPEC.
        void SaveBook()
        {
            int id = ParseInt(Request["id"]);
            string title = (Request["title"] + "").Trim();
            string author = (Request["author"] + "").Trim();
            string genre = (Request["genre"] + "").Trim();
            int year = ParseInt(Request["year"]);
            int inStock = (Request["in_stock"] + "").Trim() == "1" ? 1 : 0;

            // Validation
            if (string.IsNullOrEmpty(title))
            {
                ApiHelper.WriteError("Title is required.");
                return;
            }
            if (string.IsNullOrEmpty(author))
            {
                ApiHelper.WriteError("Author is required.");
                return;
            }
            int maxYear = DateTime.Now.Year + 1;
            if (year < 1400 || year > maxYear)
            {
                ApiHelper.WriteError("Year must be between 1400 and " + maxYear + ".");
                return;
            }

            obBook b = new obBook();
            b.Id = id;
            b.Title = title;
            b.Author = author;
            b.Genre = genre;
            b.Year = year;
            b.InStock = inStock;

            if (id > 0)
            {
                BookDb.Update(b);
                ApiHelper.WriteSuccess("Book updated.");
            }
            else
            {
                b.DateCreated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                BookDb.Insert(b);
                ApiHelper.WriteSuccess("Book added.");
            }
        }

        // Delete by id.
        void DeleteBook()
        {
            int id = ParseInt(Request["id"]);
            if (id <= 0)
            {
                ApiHelper.WriteError("Invalid id.");
                return;
            }

            BookDb.Delete(id);
            ApiHelper.WriteSuccess("Book deleted.");
        }

        static int ParseInt(string raw)
        {
            int val;
            if (int.TryParse((raw + "").Trim(), out val))
                return val;
            return 0;
        }
    }
}
