<%@ Application Language="C#" %>
<script runat="server">
    // ===== Pageless Book Library — all logic inline in Global.asax =====
    // No .aspx, no server controls, no ViewState. Every request is routed in
    // Application_BeginRequest and the response is built with StringBuilder.
    // In-memory store (no SQLite on the Linux host).

    public class Book {
        public int Id; public string Title = ""; public string Author = "";
        public string Genre = ""; public int Year; public bool InStock;
    }
    static System.Collections.Generic.List<Book> _books;
    static int _nextId = 1;
    static readonly object _lock = new object();

    static void EnsureSeed() {
        lock (_lock) {
            if (_books != null) return;
            _books = new System.Collections.Generic.List<Book>();
            _books.Add(new Book { Id = _nextId++, Title = "The Pragmatic Programmer", Author = "Andrew Hunt", Genre = "Non-Fiction", Year = 1999, InStock = true });
            _books.Add(new Book { Id = _nextId++, Title = "Dune", Author = "Frank Herbert", Genre = "Sci-Fi", Year = 1965, InStock = true });
            _books.Add(new Book { Id = _nextId++, Title = "Clean Code", Author = "Robert C. Martin", Genre = "Non-Fiction", Year = 2008, InStock = false });
            _books.Add(new Book { Id = _nextId++, Title = "The Hobbit", Author = "J.R.R. Tolkien", Genre = "Fantasy", Year = 1937, InStock = true });
        }
    }

    void Application_BeginRequest(object sender, System.EventArgs e) {
        EnsureSeed();
        string path = (Request.Path ?? "").ToLower().TrimEnd('/');
        if (path == "" || path == "/") { RenderPage(); return; }
        if (path == "/bookapi") { HandleApi(); return; }
        // anything else falls through to the host (static files like /site.css)
    }

    void Done() { Context.ApplicationInstance.CompleteRequest(); }

    static string H(string s) { return System.Web.HttpUtility.HtmlEncode(s ?? ""); }
    static string J(string s) {
        if (s == null) return "";
        System.Text.StringBuilder b = new System.Text.StringBuilder();
        foreach (char c in s) {
            if (c == '"' || c == '\\') { b.Append('\\'); b.Append(c); }
            else if (c == '\n') b.Append("\\n");
            else if (c == '\r') b.Append("\\r");
            else if (c == '\t') b.Append("\\t");
            else b.Append(c);
        }
        return b.ToString();
    }

    Book Find(int id) { lock (_lock) { foreach (Book b in _books) if (b.Id == id) return b; } return null; }

    // ---------- API ----------
    void HandleApi() {
        string action = (Request["action"] ?? "").Trim().ToLower();
        if (action == "list") ListBooks();
        else if (action == "get") GetBook();
        else if (action == "save") SaveBook();
        else if (action == "delete") DeleteBook();
        else { Response.ContentType = "application/json"; Response.Write("{\"success\":false,\"message\":\"Unknown action\"}"); }
        Done();
    }

    void ListBooks() {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("<table class='grid'><thead><tr><th>Title</th><th>Author</th><th>Genre</th><th>Year</th><th>In Stock</th><th>Actions</th></tr></thead><tbody>");
        lock (_lock) {
            for (int i = _books.Count - 1; i >= 0; i--) {
                Book b = _books[i];
                string badge = b.InStock ? "<span class='badge badge-yes'>Yes</span>" : "<span class='badge badge-no'>No</span>";
                sb.Append("<tr><td>" + H(b.Title) + "</td><td>" + H(b.Author) + "</td><td>" + H(b.Genre) + "</td><td>" + b.Year + "</td><td>" + badge + "</td>");
                sb.Append("<td class='actions'><button type='button' class='link-action' onclick='editBook(" + b.Id + ")'>Edit</button> ");
                sb.Append("<button type='button' class='link-action link-danger' onclick='deleteBook(" + b.Id + ")'>Delete</button></td></tr>");
            }
        }
        sb.Append("</tbody></table>");
        Response.ContentType = "text/html; charset=utf-8";
        Response.Write(sb.ToString());
    }

    void GetBook() {
        int id; int.TryParse(Request["id"] + "", out id);
        Book b = Find(id);
        Response.ContentType = "application/json";
        if (b == null) { Response.Write("{\"success\":false,\"message\":\"Not found\"}"); return; }
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("{\"success\":true,\"book\":{");
        sb.Append("\"id\":" + b.Id + ",");
        sb.Append("\"title\":\"" + J(b.Title) + "\",");
        sb.Append("\"author\":\"" + J(b.Author) + "\",");
        sb.Append("\"genre\":\"" + J(b.Genre) + "\",");
        sb.Append("\"year\":" + b.Year + ",");
        sb.Append("\"in_stock\":" + (b.InStock ? "true" : "false"));
        sb.Append("}}");
        Response.Write(sb.ToString());
    }

    void SaveBook() {
        Response.ContentType = "application/json";
        string title = (Request["title"] + "").Trim();
        string author = (Request["author"] + "").Trim();
        string genre = (Request["genre"] + "").Trim();
        int year; int.TryParse(Request["year"] + "", out year);
        bool inStock = (Request["in_stock"] + "") == "1" || (Request["in_stock"] + "").ToLower() == "true";

        if (title.Length == 0) { Response.Write("{\"success\":false,\"message\":\"Title is required\"}"); return; }
        if (author.Length == 0) { Response.Write("{\"success\":false,\"message\":\"Author is required\"}"); return; }
        if (year < 1400 || year > System.DateTime.Now.Year + 1) { Response.Write("{\"success\":false,\"message\":\"Year must be between 1400 and " + (System.DateTime.Now.Year + 1) + "\"}"); return; }

        int id; int.TryParse(Request["id"] + "", out id);
        Book b = id > 0 ? Find(id) : null;
        bool isNew = b == null;
        if (isNew) { b = new Book(); lock (_lock) { b.Id = _nextId++; _books.Add(b); } }
        b.Title = title; b.Author = author; b.Genre = genre; b.Year = year; b.InStock = inStock;
        Response.Write("{\"success\":true,\"message\":\"" + (isNew ? "Book added" : "Book updated") + "\"}");
    }

    void DeleteBook() {
        Response.ContentType = "application/json";
        int id; int.TryParse(Request["id"] + "", out id);
        lock (_lock) { Book b = Find(id); if (b != null) _books.Remove(b); }
        Response.Write("{\"success\":true,\"message\":\"Book deleted\"}");
    }

    // ---------- Page ----------
    void RenderPage() {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append(@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8' />
<meta name='viewport' content='width=device-width, initial-scale=1' />
<title>Book Library - Pageless (Linux)</title>
<link rel='stylesheet' href='/site.css' />
</head>
<body>
<header class='app-header'><div class='inner'>
<h1>Book Library</h1><span class='tag'>Pageless &middot; OpenWebForms / Linux</span>
</div></header>
<div class='container'>
<div class='two-col'>
<div class='card'>
<h2>Add / Edit Book</h2>
<div id='msg'></div>
<input type='hidden' id='bk-id' />
<div class='field'><label>Title</label><input type='text' id='bk-title' /></div>
<div class='field'><label>Author</label><input type='text' id='bk-author' /></div>
<div class='field'><label>Genre</label>
<select id='bk-genre'>
<option>Fiction</option><option>Non-Fiction</option><option>Sci-Fi</option>
<option>Fantasy</option><option>Biography</option><option>Mystery</option>
</select></div>
<div class='field'><label>Year</label><input type='text' id='bk-year' /></div>
<div class='field'><span class='inline-check'><input type='checkbox' id='bk-instock' /> <label for='bk-instock'>In stock</label></span></div>
<div class='btn-row'>
<button type='button' class='btn' onclick='saveBook()'>Save</button>
<button type='button' class='btn btn-secondary' onclick='clearForm()'>Clear</button>
</div>
</div>
<div class='card'><h2>Books</h2><div id='book-list'>Loading...</div></div>
</div>
</div>
<script>
const API = '/bookapi';
function showMsg(t, ok){ const m=document.getElementById('msg'); m.innerHTML = ""<div class='msg ""+(ok?'msg-success':'msg-error')+""'>""+t+""</div>""; }
async function loadBooks(){
  const r = await fetch(API + '?action=list');
  document.getElementById('book-list').innerHTML = await r.text();
}
async function saveBook(){
  const p = new URLSearchParams();
  p.append('action','save');
  p.append('id', document.getElementById('bk-id').value);
  p.append('title', document.getElementById('bk-title').value.trim());
  p.append('author', document.getElementById('bk-author').value.trim());
  p.append('genre', document.getElementById('bk-genre').value);
  p.append('year', document.getElementById('bk-year').value.trim());
  p.append('in_stock', document.getElementById('bk-instock').checked ? '1' : '0');
  const r = await fetch(API, { method:'POST', body:p });
  const d = await r.json();
  showMsg(d.message, d.success);
  if (d.success){ clearForm(); loadBooks(); }
}
function editBook(id){
  fetch(API + '?action=get&id=' + id).then(r=>r.json()).then(d=>{
    if(!d.success){ showMsg(d.message,false); return; }
    const b=d.book;
    document.getElementById('bk-id').value=b.id;
    document.getElementById('bk-title').value=b.title;
    document.getElementById('bk-author').value=b.author;
    document.getElementById('bk-genre').value=b.genre;
    document.getElementById('bk-year').value=b.year;
    document.getElementById('bk-instock').checked=b.in_stock;
    showMsg('Editing: ' + b.title, true);
  });
}
function deleteBook(id){
  if(!confirm('Delete this book?')) return;
  const p=new URLSearchParams(); p.append('action','delete'); p.append('id',id);
  fetch(API,{method:'POST',body:p}).then(r=>r.json()).then(d=>{ showMsg(d.message,d.success); loadBooks(); });
}
function clearForm(){
  document.getElementById('bk-id').value='';
  document.getElementById('bk-title').value='';
  document.getElementById('bk-author').value='';
  document.getElementById('bk-genre').selectedIndex=0;
  document.getElementById('bk-year').value='';
  document.getElementById('bk-instock').checked=false;
}
document.addEventListener('DOMContentLoaded', loadBooks);
</scr" + "ipt>" + @"
</body>
</html>");
        Response.ContentType = "text/html; charset=utf-8";
        Response.Write(sb.ToString());
        Done();
    }
</script>
