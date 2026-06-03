using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier-5b: Repeater bound to a List<T> and a string[] using a programmatically-built ITemplate
    // (a Label whose DataBinding handler calls DataBinder.Eval against the RepeaterItem.DataItem).
    // Verifies one item block per row with the bound values, plus HeaderTemplate and
    // SeparatorTemplate placement. Driven INSIDE the ALC. Deterministic.
    public class RepeaterTests
    {
        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        [Fact]
        public void RepeaterBindsListWithHeaderItemSeparatorTemplates()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.DataBindingWorker", "RepeaterListBind");

            int itemCount = (int)r[0];
            string html = (string)r[1];
            int rowCount = (int)r[2];
            bool headerPresent = (bool)r[3];
            int separatorCount = (int)r[4];
            bool hasHammer = (bool)r[5];
            bool hasNail = (bool)r[6];

            Assert.Equal(3, itemCount);                 // 3 data items -> 3 RepeaterItems
            Assert.Equal(3, rowCount);                  // one "row:" block per item
            Assert.True(headerPresent, "HeaderTemplate should render once");
            Assert.Equal(2, separatorCount);            // separators between items = itemCount - 1
            Assert.True(hasHammer, "item value 'row:Hammer' should render via DataBinder.Eval");
            Assert.True(hasNail, "item value 'row:Nail' should render via DataBinder.Eval");
            // Header must precede the first bound item in the markup.
            int headerIdx = html.IndexOf("HEADER", StringComparison.Ordinal);
            int firstRowIdx = html.IndexOf("row:Hammer", StringComparison.Ordinal);
            Assert.True(headerIdx >= 0 && firstRowIdx > headerIdx, "header must render before items");
        }

        [Fact]
        public void RepeaterBindsStringArrayWholeItem()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.DataBindingWorker", "RepeaterArrayBind");

            int itemCount = (int)r[0];
            bool hasAlpha = (bool)r[1];
            bool hasBeta = (bool)r[2];
            int separatorCount = (int)r[3];

            Assert.Equal(2, itemCount);
            Assert.True(hasAlpha, "bound 'row:alpha' should render");
            Assert.True(hasBeta, "bound 'row:beta' should render");
            Assert.Equal(1, separatorCount);            // one separator between two items
        }
    }
}
