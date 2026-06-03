using Xunit;

namespace System.Web.Tests
{
    // Tier-8 "StylingMaster" master-page merge, driven INSIDE the ALC against OUR clean-room
    // System.Web. A content page registers its <asp:Content> templates, a master with chrome around a
    // ContentPlaceHolder is applied (Page.ApplyMasterPage), and the rendered page contains the master
    // chrome wrapped around the page's merged content, with the placeholder's default content replaced.
    public class MasterPageRenderTests
    {
        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        [Fact]
        public void ContentPageMergesWithMasterChrome()
        {
            object[] r = (object[])Web.RunInAlc(
                "System.Web.Tests.MasterPageRenderWorker", "ContentPageMergesWithMaster");

            Assert.True((bool)r[1], "rendered page should contain master header chrome");
            Assert.True((bool)r[2], "rendered page should contain master footer chrome");
            Assert.True((bool)r[3], "rendered page should contain the content page's content");
            Assert.True((bool)r[4], "the placeholder's default content should be replaced");
            Assert.True((bool)r[5], "content should render between the master header and footer");
            Assert.True((bool)r[6], "the master should be the page's single child after ApplyMasterPage");
        }
    }
}
