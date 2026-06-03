using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier-5b-2ii: navigation controls (Menu / TreeView), driven INSIDE the ALC. Deterministic.
    //   * Menu with static MenuItems renders nested <ul>/<li> markup with the item text and a
    //     child nested under its parent.
    //   * TreeView with static TreeNodes renders the node text and the parent/child hierarchy.
    public class NavigationTests
    {
        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        [Fact]
        public void MenuStaticItemsRenderNestedMarkupWithText()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.NavigationWorker", "MenuStaticItems");

            Assert.Equal(2, (int)r[0]);                 // two top-level items
            Assert.True((bool)r[1], "Menu should render a <ul>");
            Assert.True((bool)r[2], "Menu should render <li> items");
            Assert.True((bool)r[3], "item text 'File' should render");
            Assert.True((bool)r[4], "item text 'Edit' should render");
            Assert.True((bool)r[5], "child item text 'Undo' should render");
            Assert.True((bool)r[6], "'Undo' should render nested after its parent 'Edit'");
            Assert.Equal(2, (int)r[7]);                 // outer list + one nested sub-list
        }

        [Fact]
        public void TreeViewStaticNodesRenderHierarchyWithText()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.NavigationWorker", "TreeViewStaticNodes");

            Assert.Equal(2, (int)r[0]);                 // two root nodes
            Assert.True((bool)r[1], "TreeView should render a <div> container");
            Assert.True((bool)r[2], "node text 'Root1' should render");
            Assert.True((bool)r[3], "node text 'Root2' should render");
            Assert.True((bool)r[4], "child node text 'Child2' should render");
            Assert.True((bool)r[5], "'Child2' should render after its parent 'Root2'");
            Assert.Equal(1, (int)r[6]);                 // Root2 has one child
            Assert.Equal(3, (int)r[7]);                 // 2 roots + 1 child
        }

        [Fact]
        public void MenuAppliesItemAndSelectedStyles()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.NavigationWorker", "MenuStyledItems");

            Assert.True((bool)r[0], "StaticMenuItemStyle CssClass should render");
            Assert.True((bool)r[1], "StaticMenuItemStyle BackColor should render a background-color");
            Assert.True((bool)r[2], "StaticSelectedStyle ForeColor should apply to the selected item");
        }

        [Fact]
        public void TreeViewAppliesNodeStylesAndShowLines()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.NavigationWorker", "TreeViewStyledNodes");

            Assert.True((bool)r[0], "NodeStyle CssClass should render on nodes");
            Assert.True((bool)r[1], "ShowLines should emit its marker class");
            Assert.True((bool)r[2], "SelectedNodeStyle ForeColor should apply to the selected node");
        }
    }
}
