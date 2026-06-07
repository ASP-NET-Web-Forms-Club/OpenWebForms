<%@ Page Language="C#" AutoEventWireup="true" %>
<script runat="server">
    public class Book { public int Id { get; set; } public string Title { get; set; } public bool InStock { get; set; } }
    protected void Page_Load(object sender, System.EventArgs e) {
        if (!IsPostBack) {
            System.Collections.Generic.List<Book> list = new System.Collections.Generic.List<Book>();
            list.Add(new Book { Id = 1, Title = "Dune", InStock = true });
            list.Add(new Book { Id = 2, Title = "Clean Code", InStock = false });
            grid.DataSource = list; grid.DataBind();
        }
    }
    protected void grid_RowCommand(object sender, System.Web.UI.WebControls.GridViewCommandEventArgs e) { }
</script>
<!DOCTYPE html>
<html><head><title>probeC</title></head><body>
    <form id="form1" runat="server">
        <h1>Probe C - GridView BoundField + TemplateField + Eval</h1>
        <asp:GridView ID="grid" runat="server" AutoGenerateColumns="false" DataKeyNames="Id" OnRowCommand="grid_RowCommand">
            <Columns>
                <asp:BoundField DataField="Title" HeaderText="Title" />
                <asp:TemplateField HeaderText="In Stock">
                    <ItemTemplate>
                        <span class='badge <%# ((bool)Eval("InStock")) ? "yes" : "no" %>'><%# ((bool)Eval("InStock")) ? "Yes" : "No" %></span>
                    </ItemTemplate>
                </asp:TemplateField>
                <asp:TemplateField HeaderText="Actions">
                    <ItemTemplate>
                        <asp:LinkButton runat="server" CommandName="EditBook" CommandArgument='<%# Eval("Id") %>' Text="Edit" />
                    </ItemTemplate>
                </asp:TemplateField>
            </Columns>
        </asp:GridView>
    </form>
</body></html>
