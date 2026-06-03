using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier-5b-2ii: Calendar / MasterPage+Content / XmlDataSource, driven INSIDE the ALC. Deterministic.
    //   * Calendar renders a month table for a VisibleDate with day cells present.
    //   * MasterPage + ContentPlaceHolder + Content merge programmatically.
    //   * XmlDataSource over an inline XML string feeds a TreeView (yields the hierarchy).
    public class MiscMasterTests
    {
        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        [Fact]
        public void CalendarRendersMonthTableForVisibleDate()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.MiscMasterWorker", "CalendarMonth");

            Assert.True((bool)r[0], "Calendar should render a <table>");
            Assert.True((int)r[1] >= 6, "Calendar should render at least 6 week rows");
            Assert.True((int)r[2] >= 42, "Calendar should render at least 42 day cells (6x7 grid)");
            Assert.True((bool)r[3], "the 15th day cell should render");
            Assert.True((bool)r[4], "the month title 'June' should render");
            Assert.True((bool)r[5], "the 30th day cell should render (June has 30 days)");
        }

        [Fact]
        public void MasterPageContentPlaceHolderMergeProgrammatically()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.MiscMasterWorker", "MasterContentMerge");

            Assert.True((bool)r[0], "master header literal should render");
            Assert.True((bool)r[1], "master footer literal should render");
            Assert.True((bool)r[2], "supplied content should render into the placeholder");
            Assert.True((bool)r[3], "default placeholder content should be replaced");
            Assert.True((bool)r[4], "content should render between the master header and footer");
            Assert.Equal(1, (int)r[5]);                 // placeholder holds the single content literal
        }

        [Fact]
        public void XmlDataSourceFeedsTreeViewHierarchy()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.MiscMasterWorker", "XmlDataSourceTree");

            Assert.Equal(2, (int)r[0]);                 // two <Folder> elements under <Root>
            Assert.Equal("Folder", (string)r[1]);       // first hierarchy node's element name
            Assert.True((bool)r[2], "first folder (Docs) should report HasChildren");
            Assert.Equal(2, (int)r[3]);                 // TreeView gets two root nodes
            Assert.Equal(2, (int)r[4]);                 // first root node has two children
            Assert.Equal(4, (int)r[5]);                 // 2 roots + 2 children of the first
        }
    }
}
