using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace System.Web.Tests
{
    // Workers executed INSIDE the custom AssemblyLoadContext (via RunInAlc) so the data-binding
    // types (DataBinder / Repeater / GridView / ObjectDataSource) bind to OUR clean-room
    // System.Web rather than the shared-framework facade.
    //
    // Covers Tier-5b data binding:
    //   * DataBinder.Eval over a POCO dotted path and a DataRowView, with a format string.
    //   * Repeater bound to a List<T> / string[] with a programmatic ITemplate that uses
    //     DataBinder.Eval; HeaderTemplate / SeparatorTemplate honored.
    //   * GridView auto-generate columns + explicit BoundField over a DataTable and a List<T>;
    //     header + one row per record + cell values; sort + page via the public command API which
    //     routes through the same HandleSort/HandlePage path that an IPostBackEventHandler postback
    //     drives.
    //   * ObjectDataSource TypeName + SelectMethod returning a List<T> feeds a GridView and a
    //     direct Select(), including a parameterized select.
    internal static class DataBindingWorker
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

        // ===================== DataBinder =====================

        public sealed class Address { public string City { get; set; } }
        public sealed class Person
        {
            public string Name { get; set; }
            public int Age { get; set; }
            public decimal Salary { get; set; }
            public Address Home { get; set; }
        }

        // Returns object[]:
        //   [0] Eval Name (string)              -> "Ada"
        //   [1] Eval dotted Home.City (string)  -> "London"
        //   [2] Eval Age (int boxed)            -> 36
        //   [3] Eval Salary with format         -> contains "1" and "234" (currency, culture-stable digits)
        //   [4] Eval on DataRowView column      -> "Widget"
        //   [5] Eval on DataRowView with format -> "Qty: 7"
        //   [6] Eval null container             -> null
        public static object[] DataBinderEval()
        {
            Person p = new Person
            {
                Name = "Ada",
                Age = 36,
                Salary = 1234.50m,
                Home = new Address { City = "London" }
            };

            object name = DataBinder.Eval(p, "Name");
            object city = DataBinder.Eval(p, "Home.City");
            object age = DataBinder.Eval(p, "Age");
            // Use an invariant, culture-stable format so the assertion is deterministic.
            string salary = DataBinder.Eval(p, "Salary", "[{0:0.00}]");

            DataTable dt = new DataTable();
            dt.Columns.Add("Product", typeof(string));
            dt.Columns.Add("Qty", typeof(int));
            dt.Rows.Add("Widget", 7);
            DataView dv = dt.DefaultView;
            DataRowView drv = dv[0];

            object prod = DataBinder.Eval(drv, "Product");
            string qty = DataBinder.Eval(drv, "Qty", "Qty: {0}");

            object nullEval = DataBinder.Eval(null, "Anything");

            return new object[] { name, city, age, salary, prod, qty, nullEval };
        }

        // ===================== Repeater =====================

        // A template that instantiates a Label and binds its Text via DataBinder.Eval against the
        // containing RepeaterItem's DataItem. The expression is supplied at construction.
        private sealed class EvalTemplate : ITemplate
        {
            private readonly string _prefix;
            private readonly string _expression; // null => bind the whole DataItem (string[] case)

            public EvalTemplate(string prefix, string expression)
            {
                _prefix = prefix;
                _expression = expression;
            }

            public void InstantiateIn(Control container)
            {
                Label l = new Label();
                l.DataBinding += (sender, e) =>
                {
                    Control bound = (Control)sender;
                    object dataItem = DataBinder.GetDataItem(bound.NamingContainer);
                    string value;
                    if (_expression == null)
                    {
                        value = dataItem == null ? string.Empty : dataItem.ToString();
                    }
                    else
                    {
                        object v = DataBinder.Eval(dataItem, _expression);
                        value = v == null ? string.Empty : v.ToString();
                    }
                    ((Label)bound).Text = _prefix + value;
                };
                container.Controls.Add(l);
            }
        }

        // A static-literal template (no data binding) for header / separator.
        private sealed class LiteralTemplate : ITemplate
        {
            private readonly string _text;
            public LiteralTemplate(string text) { _text = text; }
            public void InstantiateIn(Control container)
            {
                container.Controls.Add(new LiteralControl(_text));
            }
        }

        public sealed class Widget { public string Name { get; set; } public int Stock { get; set; } }

        // Repeater over a List<Widget> with Header/Item/Separator templates using DataBinder.Eval.
        // Returns object[]:
        //   [0] item count (Items.Count)        -> 3
        //   [1] rendered html
        //   [2] count of "row:" occurrences     -> 3 (one per item)
        //   [3] header present (bool)
        //   [4] separator count                 -> 2 (itemCount-1)
        //   [5] contains bound value "row:Hammer" (bool)
        //   [6] contains bound value "row:Nail" (bool)
        public static object[] RepeaterListBind()
        {
            Repeater rep = new Repeater();
            rep.HeaderTemplate = new LiteralTemplate("<h>HEADER</h>");
            rep.ItemTemplate = new EvalTemplate("row:", "Name");
            rep.SeparatorTemplate = new LiteralTemplate("|SEP|");
            Init(rep);

            List<Widget> data = new List<Widget>();
            data.Add(new Widget { Name = "Hammer", Stock = 3 });
            data.Add(new Widget { Name = "Nail", Stock = 100 });
            data.Add(new Widget { Name = "Saw", Stock = 5 });
            rep.DataSource = data;
            rep.DataBind();

            string html = Render(rep);
            int rowCount = CountOccurrences(html, "row:");
            int sepCount = CountOccurrences(html, "|SEP|");

            return new object[]
            {
                rep.Items.Count,
                html,
                rowCount,
                html.Contains("HEADER"),
                sepCount,
                html.Contains("row:Hammer"),
                html.Contains("row:Nail"),
            };
        }

        // Repeater over a string[] using whole-item binding (no property path).
        // Returns object[]:
        //   [0] item count          -> 2
        //   [1] contains "row:alpha"
        //   [2] contains "row:beta"
        //   [3] separator count     -> 1
        public static object[] RepeaterArrayBind()
        {
            Repeater rep = new Repeater();
            rep.ItemTemplate = new EvalTemplate("row:", null);
            rep.SeparatorTemplate = new LiteralTemplate("--");
            Init(rep);

            string[] data = new string[] { "alpha", "beta" };
            rep.DataSource = data;
            rep.DataBind();

            string html = Render(rep);
            return new object[]
            {
                rep.Items.Count,
                html.Contains("row:alpha"),
                html.Contains("row:beta"),
                CountOccurrences(html, "--"),
            };
        }

        // ===================== GridView =====================

        public sealed class Employee
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Dept { get; set; }
        }

        // GridView with AutoGenerateColumns over a DataTable.
        // Returns object[]:
        //   [0] rows count               -> 3
        //   [1] html starts "<table"
        //   [2] contains header "Name"
        //   [3] contains header "Dept"
        //   [4] contains cell "Carol"
        //   [5] HeaderRow cell count     -> 3 (Id, Name, Dept)
        //   [6] first data row cell count-> 3
        //   [7] contains "<th"           (header rendered with th cells)
        public static object[] GridViewAutoGenerateDataTable()
        {
            DataTable dt = new DataTable("Emps");
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("Name", typeof(string));
            dt.Columns.Add("Dept", typeof(string));
            dt.Rows.Add(1, "Alice", "Eng");
            dt.Rows.Add(2, "Bob", "Sales");
            dt.Rows.Add(3, "Carol", "Eng");

            GridView gv = new GridView();
            gv.AutoGenerateColumns = true;
            Init(gv);
            gv.DataSource = dt;
            gv.DataBind();

            string html = Render(gv);
            int headerCells = gv.HeaderRow != null ? gv.HeaderRow.Cells.Count : -1;
            int firstRowCells = gv.Rows.Count > 0 ? gv.Rows[0].Cells.Count : -1;

            return new object[]
            {
                gv.Rows.Count,
                html.StartsWith("<table"),
                html.Contains("Name"),
                html.Contains("Dept"),
                html.Contains("Carol"),
                headerCells,
                firstRowCells,
                html.Contains("<th"),
            };
        }

        // GridView with explicit BoundFields (DataField) over a List<Employee>.
        // Returns object[]:
        //   [0] rows count                  -> 2
        //   [1] header cell count           -> 2 (only the two bound fields)
        //   [2] contains header "Employee"
        //   [3] contains header "Department"
        //   [4] contains "Eng"
        //   [5] contains the Id value ">10<" rendered (should be FALSE: Id not bound)
        //   [6] first row cell[0] text      -> "Alice"
        public static object[] GridViewBoundField()
        {
            List<Employee> list = new List<Employee>();
            list.Add(new Employee { Id = 10, Name = "Alice", Dept = "Eng" });
            list.Add(new Employee { Id = 20, Name = "Bob", Dept = "Sales" });

            GridView gv = new GridView();
            gv.AutoGenerateColumns = false;
            BoundField nameF = new BoundField(); nameF.DataField = "Name"; nameF.HeaderText = "Employee";
            BoundField deptF = new BoundField(); deptF.DataField = "Dept"; deptF.HeaderText = "Department";
            gv.Columns.Add(nameF);
            gv.Columns.Add(deptF);
            Init(gv);
            gv.DataSource = list;
            gv.DataBind();

            string html = Render(gv);
            int headerCells = gv.HeaderRow != null ? gv.HeaderRow.Cells.Count : -1;
            string firstCell = gv.Rows.Count > 0 ? gv.Rows[0].Cells[0].Text : null;

            return new object[]
            {
                gv.Rows.Count,
                headerCells,
                html.Contains("Employee"),
                html.Contains("Department"),
                html.Contains("Eng"),
                html.Contains(">10<"),
                firstCell,
            };
        }

        // GridView paging + sort-command via the public API. Paging over a raw List is applied by
        // the PagedDataSource; Sort records the sort state on the same path a "Sort$" postback uses.
        // Returns object[]:
        //   [0] default page0 row count            -> 2 (PageSize 2 of 5)
        //   [1] PageCount                          -> 3
        //   [2] page0 first Id cell                -> "1"
        //   [3] after SetPageIndex(1) first Id cell -> "3"
        //   [4] after Sort: row count on page0     -> 2 (still bound)
        //   [5] SortExpression after Sort          -> "Name"
        //   [6] SortDirection after Sort           -> "Ascending"
        //   [7] PageIndex reset to 0 by Sort       -> 0
        public static object[] GridViewSortPage()
        {
            List<Employee> Build()
            {
                List<Employee> l = new List<Employee>();
                l.Add(new Employee { Id = 1, Name = "Eve", Dept = "A" });
                l.Add(new Employee { Id = 2, Name = "Dan", Dept = "A" });
                l.Add(new Employee { Id = 3, Name = "Cleo", Dept = "B" });
                l.Add(new Employee { Id = 4, Name = "Bea", Dept = "B" });
                l.Add(new Employee { Id = 5, Name = "Amy", Dept = "C" });
                return l;
            }

            GridView gv = new GridView();
            gv.AutoGenerateColumns = false;
            gv.AllowPaging = true;
            gv.AllowSorting = true;
            gv.PageSize = 2;
            BoundField idF = new BoundField(); idF.DataField = "Id"; idF.HeaderText = "Id";
            BoundField nameF = new BoundField(); nameF.DataField = "Name"; nameF.HeaderText = "Name"; nameF.SortExpression = "Name";
            gv.Columns.Add(idF);
            gv.Columns.Add(nameF);
            Init(gv);

            gv.DataSource = Build();
            gv.DataBind();
            int page0Count = gv.Rows.Count;
            int pageCount = gv.PageCount;
            string page0First = gv.Rows.Count > 0 ? gv.Rows[0].Cells[0].Text : null;

            gv.DataSource = Build();
            gv.SetPageIndex(1);
            gv.DataBind();
            string page1First = gv.Rows.Count > 0 ? gv.Rows[0].Cells[0].Text : null;

            gv.Sort("Name", SortDirection.Ascending);
            gv.DataSource = Build();
            gv.DataBind();
            int sortedRowCount = gv.Rows.Count;
            string sortExpr = gv.SortExpression;
            string sortDir = gv.SortDirection.ToString();
            int pageIndexAfterSort = gv.PageIndex;

            return new object[]
            {
                page0Count,
                pageCount,
                page0First,
                page1First,
                sortedRowCount,
                sortExpr,
                sortDir,
                pageIndexAfterSort,
            };
        }

        // ===================== ObjectDataSource =====================

        // A type whose instance methods return a List<Employee>; the ObjectDataSource resolves it
        // via TypeName + SelectMethod. Public so reflection inside the ALC can construct it.
        public sealed class EmployeeRepository
        {
            public List<Employee> GetAll()
            {
                List<Employee> list = new List<Employee>();
                list.Add(new Employee { Id = 1, Name = "Alice", Dept = "Eng" });
                list.Add(new Employee { Id = 2, Name = "Bob", Dept = "Sales" });
                list.Add(new Employee { Id = 3, Name = "Carol", Dept = "Eng" });
                return list;
            }

            public List<Employee> GetByDept(string dept)
            {
                List<Employee> all = GetAll();
                List<Employee> filtered = new List<Employee>();
                foreach (Employee e in all)
                {
                    if (string.Equals(e.Dept, dept, StringComparison.Ordinal)) { filtered.Add(e); }
                }
                return filtered;
            }
        }

        // ObjectDataSource direct Select() + as a GridView data source (via DataSourceID) +
        // a parameterized Select().
        // Returns object[]:
        //   [0] direct Select() row count          -> 3
        //   [1] GridView rows count (DataSourceID)  -> 3
        //   [2] gv html contains "Alice"
        //   [3] gv html contains "Carol"
        //   [4] parameterized Select() row count   -> 2 (Dept=Eng)
        public static object[] ObjectDataSourceSelect()
        {
            string typeName = typeof(EmployeeRepository).AssemblyQualifiedName;

            // --- direct Select() ---
            ObjectDataSource odsDirect = new ObjectDataSource();
            odsDirect.ID = "odsDirect";
            odsDirect.TypeName = typeName;
            odsDirect.SelectMethod = "GetAll";
            Init(odsDirect);
            int directCount = 0;
            foreach (object o in odsDirect.Select()) { directCount++; }

            // --- GridView via DataSourceID under a shared naming container ---
            HostContainer host = new HostContainer();
            ObjectDataSource ods = new ObjectDataSource();
            ods.ID = "ods1";
            ods.TypeName = typeName;
            ods.SelectMethod = "GetAll";
            GridView gv = new GridView();
            gv.ID = "gv1";
            gv.AutoGenerateColumns = true;
            gv.DataSourceID = "ods1";
            host.Controls.Add(ods);
            host.Controls.Add(gv);
            Init(host);
            gv.DataBind();
            string html = Render(gv);

            // --- parameterized Select(): GetByDept("Eng") ---
            ObjectDataSource ods2 = new ObjectDataSource();
            ods2.ID = "ods2";
            ods2.TypeName = typeName;
            ods2.SelectMethod = "GetByDept";
            Init(ods2);
            ods2.SelectParameters.Add(new Parameter("dept", TypeCode.String, "Eng"));
            int paramCount = 0;
            System.Collections.IEnumerable sel2 = ods2.Select();
            if (sel2 != null) { foreach (object o in sel2) { paramCount++; } }

            return new object[]
            {
                directCount,
                gv.Rows.Count,
                html.Contains("Alice"),
                html.Contains("Carol"),
                paramCount,
            };
        }

        private sealed class HostContainer : Control, INamingContainer { }

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
    }
}
