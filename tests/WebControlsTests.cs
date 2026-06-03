using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier-5a System.Web.UI.WebControls gate. Drives WebControlsWorker INSIDE the ALC (so the
    // control types bind to OUR clean-room System.Web) and asserts on the returned facts:
    //   * Unit.Parse / ToString round-trips ("100px", "50%", empty);
    //   * Label / TextBox / Button / DropDownList render expected HTML through a control tree;
    //   * WebControl style attributes (CssClass / BackColor) render;
    //   * TextBox / CheckBox / DropDownList postback (IPostBackDataHandler.LoadPostData ->
    //     change event) over a full Page GET -> POST round-trip.
    // Deterministic and cross-platform: invariant culture, no clock, no machineKey dependence.
    public class WebControlsTests
    {
        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        [Fact]
        public void UnitParseAndToStringRoundTrip()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.WebControlsWorker", "UnitParse");
            string pxStr = (string)r[0];
            string pctStr = (string)r[1];
            double pxValue = (double)r[2];
            double pctValue = (double)r[3];
            bool emptyIsEmpty = (bool)r[4];
            string emptyStr = (string)r[5];
            string pxType = (string)r[6];
            string pctType = (string)r[7];

            Assert.Equal("100px", pxStr);
            Assert.Equal("50%", pctStr);
            Assert.Equal(100.0, pxValue);
            Assert.Equal(50.0, pctValue);
            Assert.Equal("Pixel", pxType);
            Assert.Equal("Percentage", pctType);
            Assert.True(emptyIsEmpty, "Unit.Parse(\"\") should be empty");
            Assert.Equal(string.Empty, emptyStr);
        }

        [Fact]
        public void GridViewBindsAndRenders()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.WebControlsWorker", "GridViewSmoke");
            Assert.Equal(2, (int)r[0]);             // List bind -> 2 rows
            Assert.True((bool)r[1], "rendered html should contain Alice");
            Assert.True((bool)r[2], "rendered html should contain header The Name");
            Assert.Equal(2, (int)r[3]);             // paged: page size 2 of 3 rows
            Assert.Equal(2, (int)r[4]);             // PageCount = ceil(3/2)
            Assert.True((bool)r[5], "auto-gen html should contain Name header");
            Assert.True((bool)r[6], "GridView should render a table element");
            Assert.True((bool)r[7], "page 0 should contain x");
            Assert.False((bool)r[8], "page 0 should not contain z");
        }

        [Fact]
        public void ControlsRenderExpectedHtml()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.WebControlsWorker", "RenderControls");
            string labelHtml = (string)r[0];
            string textBoxHtml = (string)r[1];
            string buttonHtml = (string)r[2];
            string dropDownHtml = (string)r[3];

            // Label -> <span id="lbl">Hello</span>
            Assert.Equal("<span id=\"lbl\">Hello</span>", labelHtml);

            // TextBox -> single-line <input type="text" ...> with name/value/id; self-closing.
            Assert.StartsWith("<input", textBoxHtml);
            Assert.Contains("type=\"text\"", textBoxHtml);
            Assert.Contains("name=\"txt\"", textBoxHtml);
            Assert.Contains("value=\"abc\"", textBoxHtml);
            Assert.Contains("id=\"txt\"", textBoxHtml);
            Assert.Contains("/>", textBoxHtml);

            // Button -> <input type="submit" name="btn" value="Go" id="btn" />
            Assert.Contains("type=\"submit\"", buttonHtml);
            Assert.Contains("name=\"btn\"", buttonHtml);
            Assert.Contains("value=\"Go\"", buttonHtml);

            // DropDownList -> <select ...> with two <option> children, second selected.
            Assert.Contains("<select", dropDownHtml);
            Assert.Contains("name=\"ddl\"", dropDownHtml);
            Assert.Contains("<option value=\"1\">One</option>", dropDownHtml);
            // The second option carries selected="selected" (attribute order: value then selected).
            Assert.Contains("<option value=\"2\" selected=\"selected\">Two</option>", dropDownHtml);
            Assert.Contains("</select>", dropDownHtml);
        }

        [Fact]
        public void StyleAttributesRender()
        {
            string html = (string)Web.RunInAlc("System.Web.Tests.WebControlsWorker", "RenderStyledLabel");
            // CssClass renders as a class attribute; BackColor renders as a background-color style.
            Assert.Contains("class=\"myClass\"", html);
            Assert.Contains("style=\"", html);
            Assert.Contains("background-color:", html);
            Assert.Contains(">X<", html);
        }

        [Fact]
        public void PostbackRaisesChangeEvents()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.WebControlsWorker", "Postback");
            bool textChanged = (bool)r[0];
            string textValue = (string)r[1];
            bool checkedChanged = (bool)r[2];
            bool isChecked = (bool)r[3];
            bool selChanged = (bool)r[4];
            string selValue = (string)r[5];

            // TextBox: posted value loaded, TextChanged fired.
            Assert.Equal("typed", textValue);
            Assert.True(textChanged, "TextBox.TextChanged did not fire");

            // CheckBox: posted "on" -> Checked true, CheckedChanged fired.
            Assert.True(isChecked, "CheckBox not checked after postback");
            Assert.True(checkedChanged, "CheckBox.CheckedChanged did not fire");

            // DropDownList: posted value "2" -> selection changed, event fired.
            Assert.Equal("2", selValue);
            Assert.True(selChanged, "DropDownList.SelectedIndexChanged did not fire");
        }
    }
}