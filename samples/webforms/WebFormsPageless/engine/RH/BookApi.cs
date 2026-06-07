using System;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace WebFormsPageless.RH
{
    // API handler — fetch CRUD endpoint at /bookapi.
    public class BookApi
    {
        public static void HandleRequest()
        {
            HttpRequest Request = HttpContext.Current.Request;
            string action = (Request["action"] + "").ToLower().Trim();

            try
            {
                switch (action)
                {
                    case "list": GetList(); break;
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

        // Server-rendered HTML fragment of all rows.
        static void GetList()
        {
            HttpResponse Response = HttpContext.Current.Response;
            List<obBook> lst = BookDb.List();

            StringBuilder sb = new StringBuilder();

            if (lst.Count == 0)
            {
                sb.Append("<p class='empty'>No books yet. Add one!</p>");
            }
            else
            {
                sb.Append(@"<table class='grid'>
    <thead>
        <tr>
            <th>Title</th><th>Author</th><th>Genre</th><th>Year</th><th>In Stock</th><th>Actions</th>
        </tr>
    </thead>
    <tbody>");

                for (int i = 0; i < lst.Count; i++)
                {
                    obBook b = lst[i];
                    string badge = b.InStock == 1
                        ? "<span class='badge badge-yes'>Yes</span>"
                        : "<span class='badge badge-no'>No</span>";

                    sb.Append("<tr>");
                    sb.Append("<td>" + HttpUtility.HtmlEncode(b.Title) + "</td>");
                    sb.Append("<td>" + HttpUtility.HtmlEncode(b.Author) + "</td>");
                    sb.Append("<td>" + HttpUtility.HtmlEncode(b.Genre) + "</td>");
                    sb.Append("<td>" + b.Year + "</td>");
                    sb.Append("<td>" + badge + "</td>");
                    sb.Append("<td class='actions'>");
                    sb.Append("<button type='button' class='link-action' onclick='editBook(" + b.Id + ")'>Edit</button>");
                    sb.Append("<button type='button' class='link-action link-danger' onclick='deleteBook(" + b.Id + ")'>Delete</button>");
                    sb.Append("</td>");
                    sb.Append("</tr>");
                }

                sb.Append("</tbody></table>");
            }

            Response.ContentType = "text/html; charset=utf-8";
            Response.Write(sb.ToString());
        }

        // JSON of one book.
        static void GetBook()
        {
            HttpRequest Request = HttpContext.Current.Request;
            int id = 0;
            int.TryParse(Request["id"] + "", out id);

            if (id <= 0)
            {
                ApiHelper.WriteError("Invalid book ID");
                return;
            }

            obBook b = BookDb.Get(id);
            if (b == null || b.Id == 0)
            {
                ApiHelper.WriteError("Book not found");
                return;
            }

            ApiHelper.WriteJson(new { success = true, book = b });
        }

        // Insert if id empty/0, else update. Server-side validation per SPEC.
        static void SaveBook()
        {
            HttpRequest Request = HttpContext.Current.Request;

            int id = 0;
            int.TryParse(Request["id"] + "", out id);

            string title = (Request["title"] + "").Trim();
            string author = (Request["author"] + "").Trim();
            string genre = (Request["genre"] + "").Trim();

            int year = 0;
            int.TryParse(Request["year"] + "", out year);

            int inStock = 0;
            int.TryParse(Request["in_stock"] + "", out inStock);
            if (inStock != 1) inStock = 0;

            // Validation
            if (string.IsNullOrEmpty(title))
            {
                ApiHelper.WriteError("Title is required");
                return;
            }
            if (string.IsNullOrEmpty(author))
            {
                ApiHelper.WriteError("Author is required");
                return;
            }
            int maxYear = DateTime.Now.Year + 1;
            if (year < 1400 || year > maxYear)
            {
                ApiHelper.WriteError("Year must be between 1400 and " + maxYear);
                return;
            }

            obBook b = new obBook();
            b.Id = id;
            b.Title = title;
            b.Author = author;
            b.Genre = genre;
            b.Year = year;
            b.InStock = inStock;

            if (id <= 0)
            {
                BookDb.Insert(b);
                ApiHelper.WriteSuccess("Book added");
            }
            else
            {
                BookDb.Update(b);
                ApiHelper.WriteSuccess("Book updated");
            }
        }

        static void DeleteBook()
        {
            HttpRequest Request = HttpContext.Current.Request;
            int id = 0;
            int.TryParse(Request["id"] + "", out id);

            if (id <= 0)
            {
                ApiHelper.WriteError("Invalid book ID");
                return;
            }

            BookDb.Delete(id);
            ApiHelper.WriteSuccess("Book deleted");
        }
    }
}
