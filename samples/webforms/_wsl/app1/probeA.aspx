<%@ Page Language="C#" AutoEventWireup="true" %>
<script runat="server">
    public class Book { public int Id { get; set; } public string Title { get; set; } public string Author { get; set; } public int Year { get; set; } }
    protected void Page_Load(object sender, System.EventArgs e) {
        if (!IsPostBack) {
            Message.Text = "hello";
            System.Collections.Generic.List<Book> list = new System.Collections.Generic.List<Book>();
            list.Add(new Book { Id = 1, Title = "Dune", Author = "Herbert", Year = 1965 });
            list.Add(new Book { Id = 2, Title = "Hobbit", Author = "Tolkien", Year = 1937 });
            grid.DataSource = list; grid.DataBind();
        }
    }
    protected void Save_Click(object sender, System.EventArgs e) { Message.Text = "saved: " + box.Text; }
</script>
<!DOCTYPE html>
<html><head><title>probeA</title></head><body>
    <form id="form1" runat="server">
        <h1>Probe A - basic controls + AutoGenerate grid</h1>
        <asp:Label ID="Message" runat="server" />
        <asp:TextBox ID="box" runat="server" />
        <asp:Button ID="b" runat="server" Text="Save" OnClick="Save_Click" />
        <asp:GridView ID="grid" runat="server" AutoGenerateColumns="true" />
    </form>
</body></html>
