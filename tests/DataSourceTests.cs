using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier-5b: ObjectDataSource with a TypeName + SelectMethod returning a List<T>.
    //   * Direct Select() yields the rows.
    //   * Wired to a GridView via DataSourceID (resolved through a shared naming container).
    //   * A parameterized SelectMethod (GetByDept) honoring SelectParameters.
    // Driven INSIDE the ALC so the ObjectDataSource resolves TypeName against the test assembly
    // loaded into the ALC (assembly-qualified name). Deterministic, no live database.
    //
    // SqlDataSource note: a live-DB or in-memory provider path is not exercised here; a
    // cross-platform SQLite shim is not trivially wired through System.Data.SqlClient, so per the
    // task this assembly asserts ObjectDataSource (the cross-platform data-source path) instead and
    // leaves SqlDataSource's live Select to a later DB-backed pass.
    public class DataSourceTests
    {
        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        [Fact]
        public void ObjectDataSourceSelectFeedsGridViewAndDirectSelect()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.DataBindingWorker", "ObjectDataSourceSelect");

            Assert.Equal(3, (int)r[0]);                 // direct Select() returns 3 rows
            Assert.Equal(3, (int)r[1]);                 // GridView bound via DataSourceID -> 3 rows
            Assert.True((bool)r[2], "GridView html should contain 'Alice'");
            Assert.True((bool)r[3], "GridView html should contain 'Carol'");
            Assert.Equal(2, (int)r[4]);                 // parameterized GetByDept("Eng") -> 2 rows
        }
    }
}
