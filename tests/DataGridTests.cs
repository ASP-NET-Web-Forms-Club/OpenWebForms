using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier-5b-2: DataGrid data binding + rendering, driven INSIDE the ALC. Deterministic.
    //   * AutoGenerateColumns over a DataTable: a <table> with a header row + one row per record,
    //     header cells, data cells (e.g. "Carol").
    //   * Explicit BoundColumn (DataField): only the declared column renders, with the cell value.
    public class DataGridTests
    {
        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        [Fact]
        public void AutoGenerateColumnsOverDataTableRendersTable()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.Tier5b2Worker", "DataGridAutoGenerate");

            Assert.Equal(3, (int)r[0]);                 // 3 data rows (Items.Count)
            Assert.True((bool)r[1], "DataGrid should render a <table>");
            Assert.True((bool)r[2], "auto-generated header should contain 'Name'");
            Assert.True((bool)r[3], "auto-generated header should contain 'Dept'");
            Assert.True((bool)r[4], "cell value 'Carol' should render");
            Assert.Equal(3, (int)r[5]);                 // header row: 3 columns (Id, Name, Dept)
            Assert.Equal(3, (int)r[6]);                 // first data row: 3 cells
            Assert.Equal(4, (int)r[7]);                 // 1 header + 3 data <tr>
        }

        [Fact]
        public void BoundColumnRendersOnlyDeclaredColumn()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.Tier5b2Worker", "DataGridBoundColumn");

            Assert.Equal(3, (int)r[0]);                 // 3 data rows
            Assert.Equal(1, (int)r[1]);                 // only the single declared BoundColumn
            Assert.True((bool)r[2], "BoundColumn HeaderText 'Worker' should render");
            Assert.True((bool)r[3], "bound value 'Alice' should render");
            Assert.False((bool)r[4], "Dept is not a declared column, 'Sales' should NOT render");
            Assert.Equal("Alice", (string)r[5]);        // first row, first bound cell
        }
    }
}
