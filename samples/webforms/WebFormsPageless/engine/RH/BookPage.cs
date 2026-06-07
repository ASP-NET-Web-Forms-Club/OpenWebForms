using System.Text;
using System.Web;

namespace WebFormsPageless.RH
{
    // Page handler — renders the full Book Library HTML page.
    public class BookPage
    {
        public static void HandleRequest()
        {
            HttpResponse Response = HttpContext.Current.Response;

            PageTemplate pt = new PageTemplate()
            {
                Title = "Book Library",
                Description = "Book Library CRUD — Pageless Web Forms.",
                ProjectLabel = "Pageless"
            };

            StringBuilder sb = new StringBuilder();
            sb.Append(pt.GenerateHtmlHeader());

            // --- Page content ---
            sb.Append(@"
    <div class='two-col'>

        <div class='card'>
            <h2>Add / Edit Book</h2>
            <div id='msg'></div>
            <input type='hidden' id='book-id' value='' />

            <div class='field'>
                <label for='book-title'>Title</label>
                <input type='text' id='book-title' />
            </div>

            <div class='field'>
                <label for='book-author'>Author</label>
                <input type='text' id='book-author' />
            </div>

            <div class='field'>
                <label for='book-genre'>Genre</label>
                <select id='book-genre'>
                    <option value='Fiction'>Fiction</option>
                    <option value='Non-Fiction'>Non-Fiction</option>
                    <option value='Sci-Fi'>Sci-Fi</option>
                    <option value='Fantasy'>Fantasy</option>
                    <option value='Biography'>Biography</option>
                    <option value='Mystery'>Mystery</option>
                </select>
            </div>

            <div class='field'>
                <label for='book-year'>Year</label>
                <input type='number' id='book-year' />
            </div>

            <div class='field inline-check'>
                <label><input type='checkbox' id='book-instock' /> In&nbsp;Stock</label>
            </div>

            <div class='btn-row'>
                <button type='button' onclick='saveBook()'>Save</button>
                <button type='button' class='btn-secondary' onclick='clearForm()'>Clear</button>
            </div>
        </div>

        <div class='card'>
            <h2>Books</h2>
            <div id='book-list'><p class='empty'>Loading…</p></div>
        </div>

    </div>
");

            // --- Page script: fetch CRUD against /bookapi ---
            sb.Append(@"
    <script>
    const API_URL = '/bookapi';

    document.addEventListener('DOMContentLoaded', loadBooks);

    function showMsg(text, ok) {
        var box = document.getElementById('msg');
        box.className = 'msg ' + (ok ? 'msg-success' : 'msg-error');
        box.textContent = text;
        if (ok) { setTimeout(function () { box.className = ''; box.textContent = ''; }, 2500); }
    }

    function clearForm() {
        document.getElementById('book-id').value = '';
        document.getElementById('book-title').value = '';
        document.getElementById('book-author').value = '';
        document.getElementById('book-genre').value = 'Fiction';
        document.getElementById('book-year').value = '';
        document.getElementById('book-instock').checked = false;
        var box = document.getElementById('msg');
        box.className = ''; box.textContent = '';
    }

    async function loadBooks() {
        try {
            var res = await fetch(API_URL + '?action=list');
            var html = await res.text();
            document.getElementById('book-list').innerHTML = html;
        } catch (e) {
            document.getElementById('book-list').innerHTML = ""<p class='empty'>Failed to load books.</p>"";
        }
    }

    async function saveBook() {
        var fd = new FormData();
        fd.append('action', 'save');
        fd.append('id', document.getElementById('book-id').value.trim());
        fd.append('title', document.getElementById('book-title').value.trim());
        fd.append('author', document.getElementById('book-author').value.trim());
        fd.append('genre', document.getElementById('book-genre').value);
        fd.append('year', document.getElementById('book-year').value.trim());
        fd.append('in_stock', document.getElementById('book-instock').checked ? '1' : '0');

        try {
            var res = await fetch(API_URL, { method: 'POST', body: fd });
            var data = await res.json();
            if (data.success) {
                showMsg(data.message, true);
                clearForm();
                loadBooks();
            } else {
                showMsg(data.message, false);
            }
        } catch (e) {
            showMsg('Something went wrong. Please try again.', false);
        }
    }

    async function editBook(id) {
        try {
            var res = await fetch(API_URL + '?action=get&id=' + id);
            var data = await res.json();
            if (!data.success) { showMsg(data.message, false); return; }
            var b = data.book;
            document.getElementById('book-id').value = b.Id;
            document.getElementById('book-title').value = b.Title;
            document.getElementById('book-author').value = b.Author;
            document.getElementById('book-genre').value = b.Genre;
            document.getElementById('book-year').value = b.Year;
            document.getElementById('book-instock').checked = (b.InStock == 1);
            window.scrollTo(0, 0);
        } catch (e) {
            showMsg('Failed to load book.', false);
        }
    }

    async function deleteBook(id) {
        if (!confirm('Delete this book?')) return;
        var fd = new FormData();
        fd.append('action', 'delete');
        fd.append('id', id);
        try {
            var res = await fetch(API_URL, { method: 'POST', body: fd });
            var data = await res.json();
            if (data.success) {
                showMsg(data.message, true);
                clearForm();
                loadBooks();
            } else {
                showMsg(data.message, false);
            }
        } catch (e) {
            showMsg('Something went wrong. Please try again.', false);
        }
    }
    </script>
");

            sb.Append(pt.GenerateHtmlFooter());

            Response.ContentType = "text/html; charset=utf-8";
            Response.Write(sb.ToString());
            ApiHelper.EndResponse();
        }
    }
}
