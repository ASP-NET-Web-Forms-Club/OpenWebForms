using System;
using System.Collections.Generic;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace WebFormsServerControls
{
    public partial class Default : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            BookDb.EnsureSchema();

            if (!IsPostBack)
            {
                LoadGrid();
            }
        }

        void LoadGrid()
        {
            gvBooks.DataSource = BookDb.List();
            gvBooks.DataBind();
        }

        // ---- Save (shared by Button and ImageButton) ----
        protected void btnSave_Click(object sender, EventArgs e)
        {
            Save();
        }

        protected void ibSave_Click(object sender, ImageClickEventArgs e)
        {
            Save();
        }

        void Save()
        {
            string title = txtTitle.Text.Trim();
            string author = txtAuthor.Text.Trim();
            string yearText = txtYear.Text.Trim();

            // ---- Validation (server-side) ----
            List<string> errors = new List<string>();

            if (title.Length == 0)
                errors.Add("Title is required.");
            if (author.Length == 0)
                errors.Add("Author is required.");

            int year = 0;
            int maxYear = DateTime.Now.Year + 1;
            if (!int.TryParse(yearText, out year))
            {
                errors.Add("Year must be a number.");
            }
            else if (year < 1400 || year > maxYear)
            {
                errors.Add("Year must be between 1400 and " + maxYear + ".");
            }

            if (errors.Count > 0)
            {
                ShowMessage(false, string.Join(" ", errors.ToArray()));
                return;
            }

            // ---- Build model from controls ----
            obBook b = new obBook();
            b.Title = title;
            b.Author = author;
            b.Genre = ddlGenre.SelectedValue;
            b.Year = year;
            b.Format = rblFormat.SelectedValue;
            b.Tags = CollectTags();
            b.Availability = rbReserved.Checked ? "Reserved" : "Available";
            b.InStock = chkInStock.Checked ? 1 : 0;

            int editId = 0;
            int.TryParse(hfId.Value, out editId);

            if (editId > 0)
            {
                b.Id = editId;
                BookDb.Update(b);
                ShowMessage(true, "Book updated.");
            }
            else
            {
                BookDb.Insert(b);
                ShowMessage(true, "Book added.");
            }

            ClearForm();
            LoadGrid();
        }

        string CollectTags()
        {
            List<string> tags = new List<string>();
            for (int i = 0; i < cblTags.Items.Count; i++)
            {
                if (cblTags.Items[i].Selected)
                    tags.Add(cblTags.Items[i].Value);
            }
            return string.Join(",", tags.ToArray());
        }

        // ---- Grid commands (Edit / Delete) ----
        protected void gvBooks_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            int id = 0;
            if (!int.TryParse(Convert.ToString(e.CommandArgument), out id))
                return;

            if (e.CommandName == "EditBook")
            {
                EditBook(id);
            }
            else if (e.CommandName == "DeleteBook")
            {
                BookDb.Delete(id);
                ClearForm();
                LoadGrid();
                ShowMessage(true, "Book deleted.");
            }
        }

        void EditBook(int id)
        {
            obBook b = BookDb.Get(id);
            if (b == null || b.Id == 0)
            {
                ShowMessage(false, "Book not found.");
                return;
            }

            hfId.Value = b.Id.ToString();
            txtTitle.Text = b.Title;
            txtAuthor.Text = b.Author;
            txtYear.Text = b.Year.ToString();

            SelectInList(ddlGenre, b.Genre);
            SelectInList(rblFormat, b.Format);

            // tags (comma-joined)
            for (int i = 0; i < cblTags.Items.Count; i++)
                cblTags.Items[i].Selected = false;
            if (!string.IsNullOrEmpty(b.Tags))
            {
                string[] parts = b.Tags.Split(',');
                for (int p = 0; p < parts.Length; p++)
                {
                    ListItem li = cblTags.Items.FindByValue(parts[p].Trim());
                    if (li != null)
                        li.Selected = true;
                }
            }

            rbReserved.Checked = (b.Availability == "Reserved");
            rbAvailable.Checked = !rbReserved.Checked;
            chkInStock.Checked = (b.InStock == 1);

            ShowMessage(true, "Editing \"" + b.Title + "\".");
        }

        static void SelectInList(ListControl list, string value)
        {
            list.ClearSelection();
            ListItem li = list.Items.FindByValue(value);
            if (li != null)
                li.Selected = true;
            else if (list.Items.Count > 0)
                list.Items[0].Selected = true;
        }

        // ---- Clear ----
        protected void lnkClear_Click(object sender, EventArgs e)
        {
            ClearForm();
            ShowMessage(true, "Form cleared.");
        }

        void ClearForm()
        {
            hfId.Value = "";
            txtTitle.Text = "";
            txtAuthor.Text = "";
            txtYear.Text = "";
            ddlGenre.ClearSelection();
            ddlGenre.SelectedIndex = 0;
            rblFormat.ClearSelection();
            rblFormat.Items[0].Selected = true;
            for (int i = 0; i < cblTags.Items.Count; i++)
                cblTags.Items[i].Selected = false;
            rbAvailable.Checked = true;
            rbReserved.Checked = false;
            chkInStock.Checked = false;
        }

        // ---- Server-rendered message into the PlaceHolder ----
        void ShowMessage(bool success, string text)
        {
            phMessage.Controls.Clear();
            string css = success ? "msg msg-success" : "msg msg-error";
            Literal lit = new Literal();
            lit.Text = "<div class=\"" + css + "\">" + Server.HtmlEncode(text) + "</div>";
            phMessage.Controls.Add(lit);
        }
    }
}
