<%@ Page Language="C#" AutoEventWireup="true" %>
<script runat="server">
    protected void Page_Load(object sender, System.EventArgs e) { }
</script>
<!DOCTYPE html>
<html><head><title>probeB</title></head><body>
    <form id="form1" runat="server">
        <h1>Probe B - form input controls</h1>
        <asp:HiddenField ID="hf" runat="server" />
        <asp:Panel ID="pnl" runat="server">
            <asp:DropDownList ID="ddl" runat="server">
                <asp:ListItem>Fiction</asp:ListItem>
                <asp:ListItem>Sci-Fi</asp:ListItem>
            </asp:DropDownList>
            <asp:RadioButtonList ID="rbl" runat="server" RepeatDirection="Horizontal">
                <asp:ListItem Selected="True">Hardcover</asp:ListItem>
                <asp:ListItem>Paperback</asp:ListItem>
            </asp:RadioButtonList>
            <asp:CheckBoxList ID="cbl" runat="server" RepeatDirection="Horizontal">
                <asp:ListItem>Bestseller</asp:ListItem>
                <asp:ListItem>Classic</asp:ListItem>
            </asp:CheckBoxList>
            <asp:RadioButton ID="rb1" runat="server" GroupName="g" Text="Available" Checked="true" />
            <asp:RadioButton ID="rb2" runat="server" GroupName="g" Text="Reserved" />
            <asp:CheckBox ID="chk" runat="server" Text="In stock" />
        </asp:Panel>
        <asp:PlaceHolder ID="ph" runat="server" />
    </form>
</body></html>
