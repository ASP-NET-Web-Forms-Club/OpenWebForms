#!/bin/bash
WW=~/owf/app1/wwwroot

cat > "$WW/b1_ddl.aspx" <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html><html><head><title>b1</title></head><body><form id="form1" runat="server">
<h1>b1 DropDownList</h1>
<asp:DropDownList ID="ddl" runat="server"><asp:ListItem>A</asp:ListItem><asp:ListItem>B</asp:ListItem></asp:DropDownList>
</form></body></html>
PAGE

cat > "$WW/b2_rbl.aspx" <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html><html><head><title>b2</title></head><body><form id="form1" runat="server">
<h1>b2 RadioButtonList</h1>
<asp:RadioButtonList ID="rbl" runat="server" RepeatDirection="Horizontal"><asp:ListItem Selected="True">A</asp:ListItem><asp:ListItem>B</asp:ListItem></asp:RadioButtonList>
</form></body></html>
PAGE

cat > "$WW/b3_cbl.aspx" <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html><html><head><title>b3</title></head><body><form id="form1" runat="server">
<h1>b3 CheckBoxList</h1>
<asp:CheckBoxList ID="cbl" runat="server" RepeatDirection="Horizontal"><asp:ListItem>A</asp:ListItem><asp:ListItem>B</asp:ListItem></asp:CheckBoxList>
</form></body></html>
PAGE

cat > "$WW/b4_rb.aspx" <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html><html><head><title>b4</title></head><body><form id="form1" runat="server">
<h1>b4 RadioButton group</h1>
<asp:RadioButton ID="rb1" runat="server" GroupName="g" Text="Available" Checked="true" />
<asp:RadioButton ID="rb2" runat="server" GroupName="g" Text="Reserved" />
</form></body></html>
PAGE

cat > "$WW/b5_chk.aspx" <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html><html><head><title>b5</title></head><body><form id="form1" runat="server">
<h1>b5 CheckBox</h1>
<asp:CheckBox ID="chk" runat="server" Text="In stock" />
</form></body></html>
PAGE

cat > "$WW/b6_panel.aspx" <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html><html><head><title>b6</title></head><body><form id="form1" runat="server">
<h1>b6 Panel</h1>
<asp:Panel ID="pnl" runat="server"><p>inside panel</p></asp:Panel>
</form></body></html>
PAGE

cat > "$WW/b7_hf.aspx" <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html><html><head><title>b7</title></head><body><form id="form1" runat="server">
<h1>b7 HiddenField</h1>
<asp:HiddenField ID="hf" runat="server" />
<asp:Label ID="L" runat="server" Text="after hidden" />
</form></body></html>
PAGE

cat > "$WW/b8_ph.aspx" <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<script runat="server">
protected void Page_Load(object sender, System.EventArgs e){ ph.Controls.Add(new System.Web.UI.LiteralControl("<p>from placeholder</p>")); }
</script>
<!DOCTYPE html><html><head><title>b8</title></head><body><form id="form1" runat="server">
<h1>b8 PlaceHolder</h1>
<asp:PlaceHolder ID="ph" runat="server" />
</form></body></html>
PAGE

cat > "$WW/c1_bound.aspx" <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<script runat="server">
public class B { public int Id { get; set; } public string Title { get; set; } }
protected void Page_Load(object sender, System.EventArgs e){ if(!IsPostBack){ var l=new System.Collections.Generic.List<B>(); l.Add(new B{Id=1,Title="Dune"}); g.DataSource=l; g.DataBind(); } }
</script>
<!DOCTYPE html><html><head><title>c1</title></head><body><form id="form1" runat="server">
<h1>c1 GridView BoundField only</h1>
<asp:GridView ID="g" runat="server" AutoGenerateColumns="false"><Columns><asp:BoundField DataField="Title" HeaderText="Title" /></Columns></asp:GridView>
</form></body></html>
PAGE

cat > "$WW/c2_tmplstatic.aspx" <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<script runat="server">
public class B { public int Id { get; set; } public string Title { get; set; } }
protected void Page_Load(object sender, System.EventArgs e){ if(!IsPostBack){ var l=new System.Collections.Generic.List<B>(); l.Add(new B{Id=1,Title="Dune"}); g.DataSource=l; g.DataBind(); } }
</script>
<!DOCTYPE html><html><head><title>c2</title></head><body><form id="form1" runat="server">
<h1>c2 GridView TemplateField static</h1>
<asp:GridView ID="g" runat="server" AutoGenerateColumns="false"><Columns><asp:TemplateField HeaderText="X"><ItemTemplate><span>static</span></ItemTemplate></asp:TemplateField></Columns></asp:GridView>
</form></body></html>
PAGE

cat > "$WW/c3_tmpleval.aspx" <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<script runat="server">
public class B { public int Id { get; set; } public string Title { get; set; } }
protected void Page_Load(object sender, System.EventArgs e){ if(!IsPostBack){ var l=new System.Collections.Generic.List<B>(); l.Add(new B{Id=1,Title="Dune"}); g.DataSource=l; g.DataBind(); } }
</script>
<!DOCTYPE html><html><head><title>c3</title></head><body><form id="form1" runat="server">
<h1>c3 GridView TemplateField Eval</h1>
<asp:GridView ID="g" runat="server" AutoGenerateColumns="false"><Columns><asp:TemplateField HeaderText="T"><ItemTemplate><span><%# Eval("Title") %></span></ItemTemplate></asp:TemplateField></Columns></asp:GridView>
</form></body></html>
PAGE

echo "wrote probes"
for p in b1_ddl b2_rbl b3_cbl b4_rb b5_chk b6_panel b7_hf b8_ph c1_bound c2_tmplstatic c3_tmpleval; do
  printf '%-16s ' "$p"
  curl -s -o /dev/null -w 'HTTP %{http_code} bytes=%{size_download}\n' "http://localhost:7011/$p.aspx"
done
