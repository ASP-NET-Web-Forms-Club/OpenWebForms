<%@ Page Language="C#" AutoEventWireup="true" %>
<script runat="server">
    // ---- In-memory store (no SQLite on the Linux host) ----
    public class Book {
        public int Id; public string Title = ""; public string Author = "";
        public string Genre = ""; public int Year; public string Format = "";
        public string Tags = ""; public string Availability = ""; public bool InStock;
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

    protected void Page_Load(object sender, System.EventArgs e) {
        EnsureSeed();
        if (!IsPostBack) BindGrid();
    }

    void BindGrid() {
        System.Collections.Generic.List<Book> list = new System.Collections.Generic.List<Book>();
        lock (_lock) { for (int i = _books.Count - 1; i >= 0; i--) list.Add(_books[i]); }
        gvBooks.DataSource = list;
        gvBooks.DataBind();
    }

    Book FindById(int id) {
        lock (_lock) { foreach (Book b in _books) if (b.Id == id) return b; }
        return null;
    }

    void ShowMsg(string text, bool ok) {
        phMsg.Controls.Clear();
        phMsg.Controls.Add(new System.Web.UI.LiteralControl(
            "<div class='msg " + (ok ? "msg-success" : "msg-error") + "'>" +
            System.Web.HttpUtility.HtmlEncode(text) + "</div>"));
    }

    // Fill template child controls here (no <%# %> data-binding expressions —
    // those render empty on the OpenWebForms host).
    protected void gvBooks_RowDataBound(object sender, System.Web.UI.WebControls.GridViewRowEventArgs e) {
        if (e.Row.RowType != System.Web.UI.WebControls.DataControlRowType.DataRow) return;
        Book b = (Book)e.Row.DataItem;
        System.Web.UI.WebControls.Literal lit = (System.Web.UI.WebControls.Literal)e.Row.FindControl("litBadge");
        if (lit != null)
            lit.Text = b.InStock ? "<span class='badge badge-yes'>Yes</span>" : "<span class='badge badge-no'>No</span>";
        System.Web.UI.WebControls.LinkButton ed = (System.Web.UI.WebControls.LinkButton)e.Row.FindControl("lnkEdit");
        if (ed != null) ed.CommandArgument = b.Id.ToString();
        System.Web.UI.WebControls.LinkButton de = (System.Web.UI.WebControls.LinkButton)e.Row.FindControl("lnkDel");
        if (de != null) de.CommandArgument = b.Id.ToString();
    }

    protected void Save_Click(object sender, System.EventArgs e) {
        string title = (txtTitle.Text ?? "").Trim();
        string author = (txtAuthor.Text ?? "").Trim();
        int year; int.TryParse((txtYear.Text ?? "").Trim(), out year);

        if (title.Length == 0) { ShowMsg("Title is required.", false); return; }
        if (author.Length == 0) { ShowMsg("Author is required.", false); return; }
        if (year < 1400 || year > System.DateTime.Now.Year + 1) { ShowMsg("Year must be between 1400 and " + (System.DateTime.Now.Year + 1) + ".", false); return; }

        System.Collections.Generic.List<string> tags = new System.Collections.Generic.List<string>();
        foreach (System.Web.UI.WebControls.ListItem li in cblTags.Items) if (li.Selected) tags.Add(li.Value);

        int id = 0; int.TryParse(hfId.Value, out id);
        Book b = id > 0 ? FindById(id) : null;
        bool isNew = b == null;
        if (isNew) { b = new Book(); lock (_lock) { b.Id = _nextId++; _books.Add(b); } }

        b.Title = title; b.Author = author; b.Genre = ddlGenre.SelectedValue; b.Year = year;
        b.Format = rblFormat.SelectedValue;
        b.Tags = string.Join(", ", tags.ToArray());
        b.Availability = rbAvailable.Checked ? "Available" : "Reserved";
        b.InStock = chkInStock.Checked;

        ClearForm();
        BindGrid();
        ShowMsg(isNew ? "Book added." : "Book updated.", true);
    }

    protected void gvBooks_RowCommand(object sender, System.Web.UI.WebControls.GridViewCommandEventArgs e) {
        int id; int.TryParse(System.Convert.ToString(e.CommandArgument), out id);
        if (id <= 0) return;
        if (e.CommandName == "EditBook") {
            Book b = FindById(id);
            if (b == null) return;
            hfId.Value = b.Id.ToString();
            txtTitle.Text = b.Title; txtAuthor.Text = b.Author; txtYear.Text = b.Year.ToString();
            if (ListHas(ddlGenre.Items, b.Genre)) ddlGenre.SelectedValue = b.Genre;
            if (ListHas(rblFormat.Items, b.Format)) rblFormat.SelectedValue = b.Format;
            string norm = ("," + b.Tags + ",").Replace(" ", "");
            foreach (System.Web.UI.WebControls.ListItem li in cblTags.Items)
                li.Selected = norm.IndexOf("," + li.Value.Replace(" ", "") + ",") >= 0;
            rbAvailable.Checked = b.Availability != "Reserved";
            rbReserved.Checked = b.Availability == "Reserved";
            chkInStock.Checked = b.InStock;
            BindGrid();
            ShowMsg("Editing \"" + b.Title + "\".", true);
        } else if (e.CommandName == "DeleteBook") {
            lock (_lock) { Book b = FindById(id); if (b != null) _books.Remove(b); }
            ClearForm(); BindGrid(); ShowMsg("Book deleted.", true);
        }
    }

    bool ListHas(System.Web.UI.WebControls.ListItemCollection items, string val) {
        foreach (System.Web.UI.WebControls.ListItem li in items) if (li.Value == val) return true;
        return false;
    }

    protected void Clear_Click(object sender, System.EventArgs e) { ClearForm(); BindGrid(); ShowMsg("Form cleared.", true); }

    void ClearForm() {
        hfId.Value = ""; txtTitle.Text = ""; txtAuthor.Text = ""; txtYear.Text = "";
        ddlGenre.SelectedIndex = 0; rblFormat.SelectedIndex = 0;
        foreach (System.Web.UI.WebControls.ListItem li in cblTags.Items) li.Selected = false;
        rbAvailable.Checked = true; rbReserved.Checked = false; chkInStock.Checked = false;
    }
</script>
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Book Library - Server Controls (Linux)</title>
    <link rel="stylesheet" href="site.css" />
</head>
<body>
    <header class="app-header"><div class="inner">
        <h1>Book Library</h1><span class="tag">Server Controls &middot; OpenWebForms / Linux</span>
    </div></header>
    <div class="container">
    <form id="form1" runat="server">
        <div class="two-col">
            <div class="card">
                <h2>Add / Edit Book</h2>
                <asp:PlaceHolder ID="phMsg" runat="server" />
                <asp:Panel ID="pnlForm" runat="server">
                    <asp:HiddenField ID="hfId" runat="server" />
                    <div class="field"><label>Title</label>
                        <asp:TextBox ID="txtTitle" runat="server" /></div>
                    <div class="field"><label>Author</label>
                        <asp:TextBox ID="txtAuthor" runat="server" /></div>
                    <div class="field"><label>Genre</label>
                        <asp:DropDownList ID="ddlGenre" runat="server">
                            <asp:ListItem>Fiction</asp:ListItem>
                            <asp:ListItem>Non-Fiction</asp:ListItem>
                            <asp:ListItem>Sci-Fi</asp:ListItem>
                            <asp:ListItem>Fantasy</asp:ListItem>
                            <asp:ListItem>Biography</asp:ListItem>
                            <asp:ListItem>Mystery</asp:ListItem>
                        </asp:DropDownList></div>
                    <div class="field"><label>Year</label>
                        <asp:TextBox ID="txtYear" runat="server" /></div>
                    <div class="field"><label>Format</label>
                        <asp:RadioButtonList ID="rblFormat" runat="server" CssClass="choice-row" RepeatDirection="Horizontal" RepeatLayout="Flow">
                            <asp:ListItem Selected="True">Hardcover</asp:ListItem>
                            <asp:ListItem>Paperback</asp:ListItem>
                            <asp:ListItem>eBook</asp:ListItem>
                        </asp:RadioButtonList></div>
                    <div class="field"><label>Tags</label>
                        <asp:CheckBoxList ID="cblTags" runat="server" CssClass="choice-row" RepeatDirection="Horizontal" RepeatLayout="Flow">
                            <asp:ListItem>Bestseller</asp:ListItem>
                            <asp:ListItem>Award-Winning</asp:ListItem>
                            <asp:ListItem>Classic</asp:ListItem>
                            <asp:ListItem>New Release</asp:ListItem>
                        </asp:CheckBoxList></div>
                    <div class="field"><label>Availability</label>
                        <span class="inline-check">
                            <asp:RadioButton ID="rbAvailable" runat="server" GroupName="avail" Text="Available" Checked="true" />
                            <asp:RadioButton ID="rbReserved" runat="server" GroupName="avail" Text="Reserved" />
                        </span></div>
                    <div class="field"><label>In Stock</label>
                        <span class="inline-check"><asp:CheckBox ID="chkInStock" runat="server" Text="In stock" /></span></div>
                    <div class="btn-row">
                        <asp:Button ID="btnSave" runat="server" Text="Save" CssClass="btn" OnClick="Save_Click" />
                        <asp:LinkButton ID="lnkClear" runat="server" Text="Clear" CssClass="btn btn-secondary" OnClick="Clear_Click" CausesValidation="false" />
                    </div>
                </asp:Panel>
            </div>
            <div class="card">
                <h2>Books</h2>
                <asp:GridView ID="gvBooks" runat="server" CssClass="grid" AutoGenerateColumns="false"
                              OnRowCommand="gvBooks_RowCommand" OnRowDataBound="gvBooks_RowDataBound" GridLines="None">
                    <Columns>
                        <asp:BoundField DataField="Title" HeaderText="Title" />
                        <asp:BoundField DataField="Author" HeaderText="Author" />
                        <asp:BoundField DataField="Genre" HeaderText="Genre" />
                        <asp:BoundField DataField="Year" HeaderText="Year" />
                        <asp:TemplateField HeaderText="In Stock">
                            <ItemTemplate><asp:Literal ID="litBadge" runat="server" /></ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Actions">
                            <ItemTemplate>
                                <asp:LinkButton ID="lnkEdit" runat="server" CssClass="link-action" CommandName="EditBook" Text="Edit" CausesValidation="false" />
                                <asp:LinkButton ID="lnkDel" runat="server" CssClass="link-action link-danger" CommandName="DeleteBook" Text="Delete" CausesValidation="false" OnClientClick="return confirm('Delete this book?');" />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </div>
    </form>
    </div>
</body>
</html>
