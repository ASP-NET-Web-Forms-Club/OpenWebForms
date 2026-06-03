using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace System.Web.Tests
{
    // Workers executed INSIDE the custom AssemblyLoadContext (via RunInAlc) so the Tier-5b-2
    // controls (DataGrid / DetailsView / FormView, DataList + RepeatInfo) bind to OUR clean-room
    // System.Web rather than the shared-framework facade.
    //
    // Covers:
    //   * DataGrid AutoGenerateColumns + explicit BoundColumn over a DataTable; <table>/header/rows.
    //   * DetailsView bound to a single record -> a vertical field table (one row per field).
    //   * FormView with an ItemTemplate -> renders the bound record.
    //   * DataList RepeatColumns=2 (RepeatInfo) -> multi-column table; single-column comparison.
    //   * BaseDataList.DataKeys (DataKeyCollection) is populated and enumerable without throwing.
    internal static class Tier5b2Worker
    {
        private static readonly BindingFlags Inst =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static void Init(Control c)
        {
            MethodInfo mi = typeof(Control).GetMethod("InitRecursive", Inst);
            mi.Invoke(c, new object[] { null });
        }

        private static string Render(Control c)
        {
            StringWriter sw = new StringWriter();
            HtmlTextWriter w = new HtmlTextWriter(sw);
            c.RenderControl(w);
            w.Flush();
            return sw.ToString();
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle)) { return 0; }
            int count = 0;
            int idx = 0;
            while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += needle.Length;
            }
            return count;
        }

        public sealed class Employee
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Dept { get; set; }
        }

        // ===================== DataGrid =====================

        // The header row is a DataGridItem added directly to the DataGrid's Controls (it is NOT
        // part of the Items collection, which holds only data rows). Locate it by ItemType.
        private static DataGridItem FindHeaderItem(DataGrid dg)
        {
            foreach (Control c in dg.Controls)
            {
                DataGridItem item = c as DataGridItem;
                if (item != null && item.ItemType == ListItemType.Header) { return item; }
            }
            return null;
        }

        private static DataTable BuildEmps()
        {
            DataTable dt = new DataTable("Emps");
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("Name", typeof(string));
            dt.Columns.Add("Dept", typeof(string));
            dt.Rows.Add(1, "Alice", "Eng");
            dt.Rows.Add(2, "Bob", "Sales");
            dt.Rows.Add(3, "Carol", "Eng");
            return dt;
        }

        // DataGrid with AutoGenerateColumns over a DataTable.
        // Returns object[]:
        //   [0] Items.Count                  -> 3
        //   [1] html starts "<table"
        //   [2] contains header "Name"
        //   [3] contains header "Dept"
        //   [4] contains cell value "Carol"
        //   [5] header cell count            -> 3 (Id, Name, Dept)
        //   [6] first data row cell count    -> 3
        //   [7] number of <tr> in output     -> 4 (1 header + 3 data)
        public static object[] DataGridAutoGenerate()
        {
            DataGrid dg = new DataGrid();
            dg.AutoGenerateColumns = true;
            Init(dg);
            dg.DataSource = BuildEmps();
            dg.DataBind();

            string html = Render(dg);

            int headerCells = -1;
            int firstRowCells = -1;
            DataGridItem header = FindHeaderItem(dg);
            if (header != null) { headerCells = header.Cells.Count; }
            if (dg.Items.Count > 0) { firstRowCells = dg.Items[0].Cells.Count; }

            int trCount = CountOccurrences(html.ToLowerInvariant(), "<tr");

            return new object[]
            {
                dg.Items.Count,
                html.StartsWith("<table"),
                html.Contains("Name"),
                html.Contains("Dept"),
                html.Contains("Carol"),
                headerCells,
                firstRowCells,
                trCount,
            };
        }

        // DataGrid with AutoGenerateColumns=false and a single explicit BoundColumn(DataField=Name).
        // Returns object[]:
        //   [0] Items.Count                  -> 3
        //   [1] header cell count            -> 1 (only the declared BoundColumn)
        //   [2] contains header "Worker"
        //   [3] contains "Alice"
        //   [4] Dept value "Sales" NOT rendered (Dept column not declared) -> false
        //   [5] first row first cell text    -> "Alice"
        public static object[] DataGridBoundColumn()
        {
            DataGrid dg = new DataGrid();
            dg.AutoGenerateColumns = false;
            BoundColumn col = new BoundColumn();
            col.DataField = "Name";
            col.HeaderText = "Worker";
            dg.Columns.Add(col);
            Init(dg);
            dg.DataSource = BuildEmps();
            dg.DataBind();

            string html = Render(dg);

            int headerCells = -1;
            DataGridItem header = FindHeaderItem(dg);
            if (header != null) { headerCells = header.Cells.Count; }
            string firstCell = dg.Items.Count > 0 ? dg.Items[0].Cells[0].Text : null;

            return new object[]
            {
                dg.Items.Count,
                headerCells,
                html.Contains("Worker"),
                html.Contains("Alice"),
                html.Contains("Sales"),
                firstCell,
            };
        }

        // ===================== DetailsView =====================

        // DetailsView bound to a single record (a one-row DataTable) with AutoGenerateRows.
        // A DetailsView lays the fields out VERTICALLY: one <tr> per field, each with a
        // header cell + value cell.
        // Returns object[]:
        //   [0] Rows.Count                   -> 3 (Id, Name, Dept fields)
        //   [1] html starts "<table"
        //   [2] contains value "Alice"
        //   [3] contains value "Eng"
        //   [4] DataItem not null
        //   [5] number of <tr>               -> 3 (one per field, vertical layout)
        //   [6] first row cell count         -> 2 (header cell + value cell)
        public static object[] DetailsViewSingleRecord()
        {
            DataTable dt = new DataTable("One");
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("Name", typeof(string));
            dt.Columns.Add("Dept", typeof(string));
            dt.Rows.Add(7, "Alice", "Eng");

            DetailsView dv = new DetailsView();
            dv.AutoGenerateRows = true;
            Init(dv);
            dv.DataSource = dt;
            dv.DataBind();

            string html = Render(dv);
            int trCount = CountOccurrences(html.ToLowerInvariant(), "<tr");
            int rowCount = dv.Rows.Count;
            int firstRowCells = rowCount > 0 ? dv.Rows[0].Cells.Count : -1;

            return new object[]
            {
                rowCount,
                html.StartsWith("<table"),
                html.Contains("Alice"),
                html.Contains("Eng"),
                dv.DataItem != null,
                trCount,
                firstRowCells,
            };
        }

        // ===================== FormView =====================

        // A template that binds a Label's Text from the FormView's DataItem via DataBinder.Eval.
        private sealed class FormItemTemplate : ITemplate
        {
            private readonly string _expr;
            public FormItemTemplate(string expr) { _expr = expr; }
            public void InstantiateIn(Control container)
            {
                Label l = new Label();
                l.DataBinding += (sender, e) =>
                {
                    Control bound = (Control)sender;
                    object dataItem = DataBinder.GetDataItem(bound.NamingContainer);
                    object v = DataBinder.Eval(dataItem, _expr);
                    ((Label)bound).Text = "fv:" + (v == null ? string.Empty : v.ToString());
                };
                container.Controls.Add(new LiteralControl("[item]"));
                container.Controls.Add(l);
            }
        }

        // FormView with an ItemTemplate bound to a single record.
        // Returns object[]:
        //   [0] html starts "<table"        (RenderOuterTable default true)
        //   [1] contains "[item]"            (template instantiated)
        //   [2] contains bound value "fv:Carol"
        //   [3] DataItem not null
        //   [4] DataItemCount                -> 1
        public static object[] FormViewItemTemplate()
        {
            List<Employee> list = new List<Employee>();
            list.Add(new Employee { Id = 9, Name = "Carol", Dept = "Eng" });

            FormView fv = new FormView();
            fv.ItemTemplate = new FormItemTemplate("Name");
            Init(fv);
            fv.DataSource = list;
            fv.DataBind();

            string html = Render(fv);

            return new object[]
            {
                html.StartsWith("<table"),
                html.Contains("[item]"),
                html.Contains("fv:Carol"),
                fv.DataItem != null,
                fv.DataItemCount,
            };
        }

        // ===================== DataList + RepeatInfo =====================

        private sealed class EvalItemTemplate : ITemplate
        {
            private readonly string _expr;
            public EvalItemTemplate(string expr) { _expr = expr; }
            public void InstantiateIn(Control container)
            {
                Label l = new Label();
                l.DataBinding += (sender, e) =>
                {
                    Control bound = (Control)sender;
                    object dataItem = DataBinder.GetDataItem(bound.NamingContainer);
                    object v = DataBinder.Eval(dataItem, _expr);
                    ((Label)bound).Text = "cell:" + (v == null ? string.Empty : v.ToString());
                };
                container.Controls.Add(l);
            }
        }

        private static DataList BuildDataList(int repeatColumns, RepeatDirection dir)
        {
            DataList dl = new DataList();
            dl.ItemTemplate = new EvalItemTemplate("Name");
            dl.RepeatColumns = repeatColumns;
            dl.RepeatDirection = dir;
            dl.DataKeyField = "Id";
            Init(dl);

            List<Employee> data = new List<Employee>();
            data.Add(new Employee { Id = 1, Name = "A", Dept = "x" });
            data.Add(new Employee { Id = 2, Name = "B", Dept = "x" });
            data.Add(new Employee { Id = 3, Name = "C", Dept = "x" });
            data.Add(new Employee { Id = 4, Name = "D", Dept = "x" });
            dl.DataSource = data;
            dl.DataBind();
            return dl;
        }

        // DataList laid out single-column vs RepeatColumns=2 (horizontal). With 4 items:
        //   single column  -> 4 <tr>, each with 1 <td>
        //   2 columns horiz -> 2 <tr>, each with 2 <td>
        // Returns object[]:
        //   [0] single-column <tr> count    -> 4
        //   [1] single-column <td> count    -> 4
        //   [2] multi-column <tr> count     -> 2
        //   [3] multi-column <td> count     -> 4
        //   [4] multi differs from single (tr counts)  -> true
        //   [5] both contain "cell:A" and "cell:D"      -> true
        //   [6] Items.Count                  -> 4
        public static object[] DataListRepeatColumns()
        {
            DataList single = BuildDataList(0, RepeatDirection.Vertical);
            string singleHtml = Render(single).ToLowerInvariant();
            int singleTr = CountOccurrences(singleHtml, "<tr");
            int singleTd = CountOccurrences(singleHtml, "<td");

            DataList multi = BuildDataList(2, RepeatDirection.Horizontal);
            string multiHtml = Render(multi).ToLowerInvariant();
            int multiTr = CountOccurrences(multiHtml, "<tr");
            int multiTd = CountOccurrences(multiHtml, "<td");

            bool bothHaveValues =
                singleHtml.Contains("cell:a") && singleHtml.Contains("cell:d") &&
                multiHtml.Contains("cell:a") && multiHtml.Contains("cell:d");

            return new object[]
            {
                singleTr,
                singleTd,
                multiTr,
                multiTd,
                singleTr != multiTr,
                bothHaveValues,
                multi.Items.Count,
            };
        }

        // BaseDataList.DataKeys is populated from DataKeyField on DataBind and enumerable
        // without throwing.
        // Returns object[]:
        //   [0] DataKeys not null
        //   [1] DataKeys.Count               -> 4
        //   [2] DataKeys[0]                   -> 1 (boxed int)
        //   [3] DataKeys[3]                   -> 4
        //   [4] enumerated count             -> 4 (GetEnumerator does not throw)
        public static object[] DataListDataKeys()
        {
            DataList dl = BuildDataList(0, RepeatDirection.Vertical);
            DataKeyCollection keys = dl.DataKeys;

            int enumCount = 0;
            foreach (object k in keys) { enumCount++; }

            return new object[]
            {
                keys != null,
                keys.Count,
                keys[0],
                keys[3],
                enumCount,
            };
        }
    }
}
