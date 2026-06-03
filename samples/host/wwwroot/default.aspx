<%@ Page Language="C#" AutoEventWireup="true" %>
<script runat="server">
    // Inline server code. A simple in-memory model bound to the GridView, plus a Button
    // click handler that copies the TextBox value into the Label -- a true server-side
    // postback event through a compiled .aspx.
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
    }

    protected void Page_Load(object sender, System.EventArgs e)
    {
        if (!IsPostBack)
        {
            Message.Text = "Enter a value and click the button.";
            System.Collections.Generic.List<Product> products =
                new System.Collections.Generic.List<Product>();
            products.Add(new Product { Id = 1, Name = "Widget", Price = 9.99m });
            products.Add(new Product { Id = 2, Name = "Gadget", Price = 19.95m });
            products.Add(new Product { Id = 3, Name = "Gizmo", Price = 4.50m });
            Grid.DataSource = products;
            Grid.DataBind();
        }
    }

    protected void Submit_Click(object sender, System.EventArgs e)
    {
        Message.Text = "You said: " + NameBox.Text;
    }
</script>
<!DOCTYPE html>
<html>
<head>
    <title>System.Web clean-room .aspx demo</title>
</head>
<body>
    <h1>Clean-room System.Web .aspx demo</h1>
    <form id="form1" runat="server">
        <p>
            <asp:Label runat="server" ID="Message" Text="" />
        </p>
        <p>
            <asp:TextBox runat="server" ID="NameBox" />
            <asp:Button runat="server" ID="Submit" Text="Submit" OnClick="Submit_Click" />
        </p>
        <h2>Products</h2>
        <asp:GridView runat="server" ID="Grid" AutoGenerateColumns="true" />
    </form>
</body>
</html>