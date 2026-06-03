using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier-5b-2: RepeatInfo multi-column layout + BaseDataList.DataKeys, driven INSIDE the ALC.
    //   * A DataList with RepeatColumns=2 / RepeatDirection=Horizontal now lays out a multi-column
    //     table: the cell/row structure differs from the single-column (RepeatColumns=0) layout.
    //   * BaseDataList.DataKeys returns the keys (DataKeyCollection) without throwing.
    public class RepeatInfoTests
    {
        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        [Fact]
        public void DataListRepeatColumnsLaysOutMultiColumnTable()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.Tier5b2Worker", "DataListRepeatColumns");

            // 4 items: single-column -> 4 rows x 1 cell; 2-column horizontal -> 2 rows x 2 cells.
            Assert.Equal(4, (int)r[0]);                 // single-column <tr> count
            Assert.Equal(4, (int)r[1]);                 // single-column <td> count
            Assert.Equal(2, (int)r[2]);                 // multi-column <tr> count
            Assert.Equal(4, (int)r[3]);                 // multi-column <td> count
            Assert.True((bool)r[4], "multi-column row structure must differ from single-column");
            Assert.True((bool)r[5], "both layouts must still render the bound item values");
            Assert.Equal(4, (int)r[6]);                 // Items.Count
        }

        [Fact]
        public void BaseDataListDataKeysReturnsKeysWithoutThrowing()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.Tier5b2Worker", "DataListDataKeys");

            Assert.True((bool)r[0], "DataKeys should not be null");
            Assert.Equal(4, (int)r[1]);                 // DataKeys.Count
            Assert.Equal(1, Convert.ToInt32(r[2]));     // DataKeys[0]
            Assert.Equal(4, Convert.ToInt32(r[3]));     // DataKeys[3]
            Assert.Equal(4, (int)r[4]);                 // enumerated count (GetEnumerator OK)
        }
    }
}
