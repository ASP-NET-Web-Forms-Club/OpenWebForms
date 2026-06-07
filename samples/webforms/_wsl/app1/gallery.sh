#!/bin/bash
WW=~/owf/app1/wwwroot

p() { cat > "$WW/$1.aspx"; }

p g_listbox <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html><html><head><title>g</title></head><body><form id="form1" runat="server">
<asp:ListBox ID="lb" runat="server" SelectionMode="Multiple" Rows="4"><asp:ListItem>One</asp:ListItem><asp:ListItem>Two</asp:ListItem></asp:ListBox>
</form></body></html>
PAGE

p g_linkbutton <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<script runat="server">protected void c(object s, System.EventArgs e){ L.Text="clicked"; }</script>
<!DOCTYPE html><html><head><title>g</title></head><body><form id="form1" runat="server">
<asp:LinkButton ID="lk" runat="server" Text="Click" OnClick="c" /><asp:Label ID="L" runat="server" />
</form></body></html>
PAGE

p g_imagebutton <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html><html><head><title>g</title></head><body><form id="form1" runat="server">
<asp:ImageButton ID="ib" runat="server" ImageUrl="site.css" AlternateText="save" />
</form></body></html>
PAGE

p g_hyperlink <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html><html><head><title>g</title></head><body><form id="form1" runat="server">
<asp:HyperLink ID="h" runat="server" NavigateUrl="http://x" Text="link" />
</form></body></html>
PAGE

p g_image <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html><html><head><title>g</title></head><body><form id="form1" runat="server">
<asp:Image ID="im" runat="server" ImageUrl="site.css" AlternateText="x" />
</form></body></html>
PAGE

p g_literal <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<script runat="server">protected void Page_Load(object s, System.EventArgs e){ lit.Text="<b>literal</b>"; }</script>
<!DOCTYPE html><html><head><title>g</title></head><body><form id="form1" runat="server">
<asp:Literal ID="lit" runat="server" />
</form></body></html>
PAGE

p g_calendar <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html><html><head><title>g</title></head><body><form id="form1" runat="server">
<asp:Calendar ID="cal" runat="server" />
</form></body></html>
PAGE

p g_repeater_eval <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<script runat="server">
protected void Page_Load(object s, System.EventArgs e){ if(!IsPostBack){ var l=new System.Collections.Generic.List<string>(); l.Add("a"); l.Add("b"); r.DataSource=l; r.DataBind(); } }
</script>
<!DOCTYPE html><html><head><title>g</title></head><body><form id="form1" runat="server">
<asp:Repeater ID="r" runat="server"><ItemTemplate><div><%# Container.DataItem %></div></ItemTemplate></asp:Repeater>
</form></body></html>
PAGE

p g_datalist_eval <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<script runat="server">
public class B { public string Title { get; set; } }
protected void Page_Load(object s, System.EventArgs e){ if(!IsPostBack){ var l=new System.Collections.Generic.List<B>(); l.Add(new B{Title="x"}); dl.DataSource=l; dl.DataBind(); } }
</script>
<!DOCTYPE html><html><head><title>g</title></head><body><form id="form1" runat="server">
<asp:DataList ID="dl" runat="server"><ItemTemplate><%# Eval("Title") %></ItemTemplate></asp:DataList>
</form></body></html>
PAGE

p g_table <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html><html><head><title>g</title></head><body><form id="form1" runat="server">
<asp:Table ID="t" runat="server"><asp:TableRow><asp:TableCell>cell</asp:TableCell></asp:TableRow></asp:Table>
</form></body></html>
PAGE

p g_multiview <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html><html><head><title>g</title></head><body><form id="form1" runat="server">
<asp:MultiView ID="mv" runat="server" ActiveViewIndex="0"><asp:View runat="server"><p>view0</p></asp:View></asp:MultiView>
</form></body></html>
PAGE

p g_validator <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html><html><head><title>g</title></head><body><form id="form1" runat="server">
<asp:TextBox ID="tb" runat="server" /><asp:RequiredFieldValidator ID="rv" runat="server" ControlToValidate="tb" ErrorMessage="req" /><asp:Button ID="b" runat="server" Text="go" />
</form></body></html>
PAGE

p g_bulleted <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html><html><head><title>g</title></head><body><form id="form1" runat="server">
<asp:BulletedList ID="bl" runat="server"><asp:ListItem>x</asp:ListItem></asp:BulletedList>
</form></body></html>
PAGE

p g_tmpl_rowdatabound <<'PAGE'
<%@ Page Language="C#" AutoEventWireup="true" %>
<script runat="server">
public class B { public int Id { get; set; } public string Title { get; set; } public bool InStock { get; set; } }
protected void Page_Load(object s, System.EventArgs e){ if(!IsPostBack){ var l=new System.Collections.Generic.List<B>(); l.Add(new B{Id=1,Title="Dune",InStock=true}); g.DataSource=l; g.DataBind(); } }
protected void g_RowDataBound(object s, System.Web.UI.WebControls.GridViewRowEventArgs e){
  if(e.Row.RowType==System.Web.UI.WebControls.DataControlRowType.DataRow){
    B b=(B)e.Row.DataItem;
    System.Web.UI.WebControls.Literal lit=(System.Web.UI.WebControls.Literal)e.Row.FindControl("litBadge");
    if(lit!=null) lit.Text = b.InStock ? "Yes" : "No";
    System.Web.UI.WebControls.LinkButton del=(System.Web.UI.WebControls.LinkButton)e.Row.FindControl("lnkDel");
    if(del!=null) del.CommandArgument = b.Id.ToString();
  }
}
protected void g_RowCommand(object s, System.Web.UI.WebControls.GridViewCommandEventArgs e){}
</script>
<!DOCTYPE html><html><head><title>g</title></head><body><form id="form1" runat="server">
<asp:GridView ID="g" runat="server" AutoGenerateColumns="false" OnRowDataBound="g_RowDataBound" OnRowCommand="g_RowCommand">
<Columns>
<asp:BoundField DataField="Title" HeaderText="Title" />
<asp:TemplateField HeaderText="Stock"><ItemTemplate><asp:Literal ID="litBadge" runat="server" /></ItemTemplate></asp:TemplateField>
<asp:TemplateField HeaderText="Act"><ItemTemplate><asp:LinkButton ID="lnkDel" runat="server" CommandName="DeleteBook" Text="Delete" /></ItemTemplate></asp:TemplateField>
</Columns></asp:GridView>
</form></body></html>
PAGE

echo "wrote gallery"
for x in g_listbox g_linkbutton g_imagebutton g_hyperlink g_image g_literal g_calendar g_repeater_eval g_datalist_eval g_table g_multiview g_validator g_bulleted g_tmpl_rowdatabound; do
  printf '%-22s ' "$x"
  curl -s -o /dev/null -w 'HTTP %{http_code} bytes=%{size_download}\n' "http://localhost:7011/$x.aspx"
done
