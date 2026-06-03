using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier-5b-2: DetailsView + FormView, driven INSIDE the ALC. Deterministic.
    //   * DetailsView bound to a single record renders a vertical field table:
    //     one <tr> per field, each with a header cell + value cell.
    //   * FormView with an ItemTemplate renders the bound record.
    public class DetailViewsTests
    {
        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        [Fact]
        public void DetailsViewBoundToSingleRecordRendersVerticalFieldTable()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.Tier5b2Worker", "DetailsViewSingleRecord");

            Assert.Equal(3, (int)r[0]);                 // 3 field rows (Id, Name, Dept)
            Assert.True((bool)r[1], "DetailsView should render a <table>");
            Assert.True((bool)r[2], "value 'Alice' should render");
            Assert.True((bool)r[3], "value 'Eng' should render");
            Assert.True((bool)r[4], "DataItem should be the bound record (not null)");
            Assert.Equal(3, (int)r[5]);                 // vertical layout: one <tr> per field
            Assert.Equal(2, (int)r[6]);                 // each field row: header cell + value cell
        }

        [Fact]
        public void FormViewWithItemTemplateRendersBoundRecord()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.Tier5b2Worker", "FormViewItemTemplate");

            Assert.True((bool)r[0], "FormView should render a <table> (RenderOuterTable default)");
            Assert.True((bool)r[1], "ItemTemplate should be instantiated ('[item]')");
            Assert.True((bool)r[2], "ItemTemplate data-binding should render 'fv:Carol'");
            Assert.True((bool)r[3], "DataItem should be the bound record (not null)");
            Assert.Equal(1, (int)r[4]);                 // DataItemCount = 1
        }
    }
}
