#!/bin/bash
WW=~/owf/app1/wwwroot

cat > "$WW/bPH.aspx" <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html><html><head><title>bPH</title></head><body><form id="form1" runat="server">
<h1>bPH empty PlaceHolder</h1>
<asp:PlaceHolder ID="ph" runat="server" />
<p>after</p>
</form></body></html>
PAGE

cat > "$WW/bPanelList.aspx" <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html><html><head><title>bPanelList</title></head><body><form id="form1" runat="server">
<h1>bPanelList: list controls inside Panel</h1>
<asp:Panel ID="pnl" runat="server">
<asp:DropDownList ID="ddl" runat="server"><asp:ListItem>A</asp:ListItem></asp:DropDownList>
<asp:RadioButtonList ID="rbl" runat="server"><asp:ListItem>A</asp:ListItem></asp:RadioButtonList>
<asp:CheckBoxList ID="cbl" runat="server"><asp:ListItem>A</asp:ListItem></asp:CheckBoxList>
</asp:Panel>
</form></body></html>
PAGE

cat > "$WW/bH1.aspx" <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html><html><head><title>bH1</title></head><body><form id="form1" runat="server">
<h1>bH1 first half of probe B</h1>
<asp:HiddenField ID="hf" runat="server" />
<asp:Panel ID="pnl" runat="server">
<asp:DropDownList ID="ddl" runat="server"><asp:ListItem>Fiction</asp:ListItem><asp:ListItem>Sci-Fi</asp:ListItem></asp:DropDownList>
<asp:RadioButtonList ID="rbl" runat="server" RepeatDirection="Horizontal"><asp:ListItem Selected="True">Hardcover</asp:ListItem><asp:ListItem>Paperback</asp:ListItem></asp:RadioButtonList>
<asp:CheckBoxList ID="cbl" runat="server" RepeatDirection="Horizontal"><asp:ListItem>Bestseller</asp:ListItem><asp:ListItem>Classic</asp:ListItem></asp:CheckBoxList>
</asp:Panel>
</form></body></html>
PAGE

cat > "$WW/bH2.aspx" <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html><html><head><title>bH2</title></head><body><form id="form1" runat="server">
<h1>bH2 second half of probe B</h1>
<asp:Panel ID="pnl" runat="server">
<asp:RadioButton ID="rb1" runat="server" GroupName="g" Text="Available" Checked="true" />
<asp:RadioButton ID="rb2" runat="server" GroupName="g" Text="Reserved" />
<asp:CheckBox ID="chk" runat="server" Text="In stock" />
</asp:Panel>
<asp:PlaceHolder ID="ph" runat="server" />
</form></body></html>
PAGE

cat > "$WW/c3b_databinder.aspx" <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<script runat="server">
public class B { public int Id { get; set; } public string Title { get; set; } }
protected void Page_Load(object sender, System.EventArgs e){ if(!IsPostBack){ var l=new System.Collections.Generic.List<B>(); l.Add(new B{Id=1,Title="Dune"}); g.DataSource=l; g.DataBind(); } }
</script>
<!DOCTYPE html><html><head><title>c3b</title></head><body><form id="form1" runat="server">
<h1>c3b TemplateField DataBinder.Eval</h1>
<asp:GridView ID="g" runat="server" AutoGenerateColumns="false"><Columns><asp:TemplateField HeaderText="T"><ItemTemplate><span><%# DataBinder.Eval(Container.DataItem, "Title") %></span></ItemTemplate></asp:TemplateField></Columns></asp:GridView>
</form></body></html>
PAGE

echo "wrote probes2"
for p in bPH bPanelList bH1 bH2 c3b_databinder; do
  printf '%-16s ' "$p"
  curl -s -o /dev/null -w 'HTTP %{http_code} bytes=%{size_download}\n' "http://localhost:7011/$p.aspx"
done
