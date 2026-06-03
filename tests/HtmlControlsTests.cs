using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier-5a System.Web.UI.HtmlControls gate. Drives HtmlControlsWorker INSIDE the ALC and
    // asserts on the returned facts:
    //   * HtmlForm renders a <form> that contains the page __VIEWSTATE hidden field (emitted
    //     exactly once, inside the form -- reconciling the Tier-4 page-level workaround);
    //   * HtmlInputText / HtmlAnchor / HtmlGenericControl render expected markup;
    //   * HtmlSelect postback (IPostBackDataHandler.LoadPostData -> ServerChange).
    // Deterministic and cross-platform.
    public class HtmlControlsTests
    {
        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        [Fact]
        public void HtmlControlsRenderExpectedMarkup()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.HtmlControlsWorker", "RenderControls");
            string inputTextHtml = (string)r[0];
            string anchorHtml = (string)r[1];
            string genericHtml = (string)r[2];

            // HtmlInputText -> <input name="name" type="text" maxlength="10" value="Bob" id="name" />
            Assert.StartsWith("<input", inputTextHtml);
            Assert.Contains("name=\"name\"", inputTextHtml);
            Assert.Contains("type=\"text\"", inputTextHtml);
            Assert.Contains("value=\"Bob\"", inputTextHtml);
            Assert.Contains("maxlength=\"10\"", inputTextHtml);
            Assert.Contains("/>", inputTextHtml);

            // HtmlAnchor -> <a id="lnk" href="http://example.com/" title="Example">click</a>
            Assert.StartsWith("<a", anchorHtml);
            Assert.Contains("href=\"http://example.com/\"", anchorHtml);
            Assert.Contains("title=\"Example\"", anchorHtml);
            Assert.Contains(">click</a>", anchorHtml);

            // HtmlGenericControl -> <section id="sec" data-role="main"><p>hi</p></section>
            Assert.StartsWith("<section", genericHtml);
            Assert.Contains("data-role=\"main\"", genericHtml);
            Assert.Contains("<p>hi</p>", genericHtml);
            Assert.Contains("</section>", genericHtml);
        }

        [Fact]
        public void HtmlFormEmitsViewStateInsideForm()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.HtmlControlsWorker", "FormRendersViewState");
            string body = (string)r[0];
            bool insideOnce = (bool)r[1];

            Assert.Contains("<form", body);
            Assert.Contains("</form>", body);
            Assert.Contains("name=\"__VIEWSTATE\"", body);
            // The hidden field is emitted exactly once and lies between <form> and </form>.
            Assert.True(insideOnce, "__VIEWSTATE must be emitted exactly once, inside the <form>");
        }

        [Fact]
        public void HtmlSelectPostbackRaisesServerChange()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.HtmlControlsWorker", "SelectPostback");
            bool serverChangeFired = (bool)r[0];
            string selectedValue = (string)r[1];

            Assert.Equal("b", selectedValue);
            Assert.True(serverChangeFired, "HtmlSelect.ServerChange did not fire on postback");
        }
    }
}
