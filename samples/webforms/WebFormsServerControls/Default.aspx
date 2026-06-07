<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="WebFormsServerControls.Default" %>

<!DOCTYPE html>
<html>
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Book Library — Server Controls</title>
    <link rel="stylesheet" href="css/site.css" />
</head>
<body>
    <form id="form1" runat="server">

    <header class="app-header">
        <div class="inner">
            <h1>📚 Book Library</h1>
            <span class="tag">Server Controls</span>
        </div>
    </header>

    <div class="container">
        <div class="two-col">

            <%-- ===================== LEFT: Add / Edit form ===================== --%>
            <div class="card">
                <h2>Add / Edit Book</h2>

                <asp:Panel ID="pnlForm" runat="server" DefaultButton="btnSave">

                    <asp:HiddenField ID="hfId" runat="server" />

                    <asp:PlaceHolder ID="phMessage" runat="server"></asp:PlaceHolder>

                    <div class="field">
                        <label>Title</label>
                        <asp:TextBox ID="txtTitle" runat="server" CssClass="input" />
                    </div>

                    <div class="field">
                        <label>Author</label>
                        <asp:TextBox ID="txtAuthor" runat="server" CssClass="input" />
                    </div>

                    <div class="field">
                        <label>Genre</label>
                        <asp:DropDownList ID="ddlGenre" runat="server" CssClass="input">
                            <asp:ListItem Text="Fiction" Value="Fiction" />
                            <asp:ListItem Text="Non-Fiction" Value="Non-Fiction" />
                            <asp:ListItem Text="Sci-Fi" Value="Sci-Fi" />
                            <asp:ListItem Text="Fantasy" Value="Fantasy" />
                            <asp:ListItem Text="Biography" Value="Biography" />
                            <asp:ListItem Text="Mystery" Value="Mystery" />
                        </asp:DropDownList>
                    </div>

                    <div class="field">
                        <label>Year</label>
                        <asp:TextBox ID="txtYear" runat="server" CssClass="input" />
                    </div>

                    <div class="field">
                        <label>Format</label>
                        <asp:RadioButtonList ID="rblFormat" runat="server" RepeatDirection="Horizontal" CssClass="choice-row">
                            <asp:ListItem Text="Hardcover" Value="Hardcover" Selected="True" />
                            <asp:ListItem Text="Paperback" Value="Paperback" />
                            <asp:ListItem Text="eBook" Value="eBook" />
                        </asp:RadioButtonList>
                    </div>

                    <div class="field">
                        <label>Tags</label>
                        <asp:CheckBoxList ID="cblTags" runat="server" RepeatDirection="Horizontal" CssClass="choice-row">
                            <asp:ListItem Text="Bestseller" Value="Bestseller" />
                            <asp:ListItem Text="Award-Winning" Value="Award-Winning" />
                            <asp:ListItem Text="Classic" Value="Classic" />
                            <asp:ListItem Text="New Release" Value="New Release" />
                        </asp:CheckBoxList>
                    </div>

                    <div class="field">
                        <label>Availability</label>
                        <span class="inline-check">
                            <asp:RadioButton ID="rbAvailable" runat="server" GroupName="Availability" Text="Available" Checked="true" />
                            <asp:RadioButton ID="rbReserved" runat="server" GroupName="Availability" Text="Reserved" />
                        </span>
                    </div>

                    <div class="field">
                        <label>In&nbsp;Stock</label>
                        <asp:CheckBox ID="chkInStock" runat="server" Text="In stock" />
                    </div>

                    <div class="actions">
                        <asp:Button ID="btnSave" runat="server" CssClass="btn btn-primary" Text="Save" OnClick="btnSave_Click" />
                        <asp:LinkButton ID="lnkClear" runat="server" CssClass="btn btn-ghost" Text="Clear" OnClick="lnkClear_Click" CausesValidation="false" />
                        <asp:ImageButton ID="ibSave" runat="server" CssClass="icon-btn" OnClick="ibSave_Click"
                            ToolTip="Save"
                            ImageUrl="data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSIyMCIgaGVpZ2h0PSIyMCIgdmlld0JveD0iMCAwIDI0IDI0IiBmaWxsPSJub25lIiBzdHJva2U9IiMyNTYzZWIiIHN0cm9rZS13aWR0aD0iMiIgc3Ryb2tlLWxpbmVjYXA9InJvdW5kIiBzdHJva2UtbGluZWpvaW49InJvdW5kIj48cGF0aCBkPSJNMTkgMjFINWEyIDIgMCAwIDEtMi0yVjVhMiAyIDAgMCAxIDItMmgxMWw1IDV2MTFhMiAyIDAgMCAxLTIgMnoiLz48cG9seWxpbmUgcG9pbnRzPSIxNyAyMSAxNyAxMyA3IDEzIDcgMjEiLz48cG9seWxpbmUgcG9pbnRzPSI3IDMgNyA4IDE1IDgiLz48L3N2Zz4=" />
                    </div>

                </asp:Panel>
            </div>

            <%-- ===================== RIGHT: Books grid ===================== --%>
            <div class="card">
                <h2>Books</h2>

                <asp:GridView ID="gvBooks" runat="server" CssClass="grid" AutoGenerateColumns="false"
                    DataKeyNames="Id" OnRowCommand="gvBooks_RowCommand" GridLines="None">
                    <Columns>
                        <asp:BoundField DataField="Title" HeaderText="Title" />
                        <asp:BoundField DataField="Author" HeaderText="Author" />
                        <asp:BoundField DataField="Genre" HeaderText="Genre" />
                        <asp:BoundField DataField="Year" HeaderText="Year" />
                        <asp:TemplateField HeaderText="In Stock">
                            <ItemTemplate>
                                <asp:Literal ID="litStock" runat="server"
                                    Text='<%# Convert.ToInt32(Eval("InStock")) == 1
                                        ? "<span class=\"badge badge-yes\">Yes</span>"
                                        : "<span class=\"badge badge-no\">No</span>" %>' />
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Actions">
                            <ItemTemplate>
                                <asp:LinkButton ID="lnkEdit" runat="server" CssClass="link-action"
                                    CommandName="EditBook" CommandArgument='<%# Eval("Id") %>'
                                    Text="Edit" CausesValidation="false" />
                                &nbsp;
                                <asp:LinkButton ID="lnkDelete" runat="server" CssClass="link-action link-danger"
                                    CommandName="DeleteBook" CommandArgument='<%# Eval("Id") %>'
                                    Text="Delete" CausesValidation="false"
                                    OnClientClick="return confirm('Delete this book?');" />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                    <EmptyDataTemplate>
                        <p class="empty">No books yet.</p>
                    </EmptyDataTemplate>
                </asp:GridView>
            </div>

        </div>
    </div>

    </form>
</body>
</html>
