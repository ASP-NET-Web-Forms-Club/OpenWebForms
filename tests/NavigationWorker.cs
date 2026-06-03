using System;
using System.IO;
using System.Reflection;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace System.Web.Tests
{
    // Workers executed INSIDE the custom AssemblyLoadContext (via RunInAlc) so the navigation
    // controls (Menu / TreeView) bind to OUR clean-room System.Web rather than the shared-framework
    // facade.
    //
    // Covers:
    //   * Menu with static MenuItems renders nested <ul>/<li> markup containing the item text,
    //     including a child item nested under its parent.
    //   * TreeView with static TreeNodes renders the node text and the parent/child hierarchy
    //     (children rendered after, and indented relative to, their parent).
    internal static class NavigationWorker
    {
        private static readonly BindingFlags Inst =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static void Init(Control c)
        {
            MethodInfo mi = typeof(Control).GetMethod("InitRecursive", Inst);
            mi.Invoke(c, new object[] { null });
        }

        private static string Render(Control c)
        {
            StringWriter sw = new StringWriter();
            HtmlTextWriter w = new HtmlTextWriter(sw);
            c.RenderControl(w);
            w.Flush();
            return sw.ToString();
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle)) { return 0; }
            int count = 0;
            int idx = 0;
            while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += needle.Length;
            }
            return count;
        }

        // Menu with two top-level items; the second has a child. With the default StaticDisplayLevels
        // only the first level is static; bump StaticDisplayLevels so the child is also rendered.
        // Returns object[]:
        //   [0] Items.Count                         -> 2 (top-level)
        //   [1] html contains "<ul"
        //   [2] html contains "<li"
        //   [3] contains "File"   (item 0 text)
        //   [4] contains "Edit"   (item 1 text)
        //   [5] contains "Undo"   (child of Edit)
        //   [6] "Undo" rendered AFTER "Edit" (nested ordering)
        //   [7] number of <ul> (outer + one nested sub-list)   -> 2
        public static object[] MenuStaticItems()
        {
            Menu menu = new Menu();
            menu.StaticDisplayLevels = 2; // render the child level statically too
            MenuItem file = new MenuItem("File", "f");
            MenuItem edit = new MenuItem("Edit", "e");
            MenuItem undo = new MenuItem("Undo", "u");
            edit.ChildItems.Add(undo);
            menu.Items.Add(file);
            menu.Items.Add(edit);

            Init(menu);
            string html = Render(menu);

            int editIdx = html.IndexOf("Edit", StringComparison.Ordinal);
            int undoIdx = html.IndexOf("Undo", StringComparison.Ordinal);

            return new object[]
            {
                menu.Items.Count,
                html.Contains("<ul"),
                html.Contains("<li"),
                html.Contains("File"),
                html.Contains("Edit"),
                html.Contains("Undo"),
                editIdx >= 0 && undoIdx > editIdx,
                CountOccurrences(html.ToLowerInvariant(), "<ul"),
            };
        }

        // TreeView with two root nodes; the second has a child. ExpandDepth is set so both levels
        // render. Verifies the node text and that the child renders after the parent.
        // Returns object[]:
        //   [0] Nodes.Count                         -> 2 (roots)
        //   [1] html starts "<div" (TreeView renders a div container)
        //   [2] contains "Root1"
        //   [3] contains "Root2"
        //   [4] contains "Child2" (child of Root2)
        //   [5] "Child2" rendered AFTER "Root2"
        //   [6] Root2 child node count              -> 1
        //   [7] total node count (roots + children) -> 3
        public static object[] TreeViewStaticNodes()
        {
            TreeView tree = new TreeView();
            tree.ExpandDepth = 10; // force children to render deterministically
            TreeNode root1 = new TreeNode("Root1", "r1");
            TreeNode root2 = new TreeNode("Root2", "r2");
            TreeNode child2 = new TreeNode("Child2", "c2");
            root2.ChildNodes.Add(child2);
            tree.Nodes.Add(root1);
            tree.Nodes.Add(root2);

            Init(tree);
            string html = Render(tree);

            int root2Idx = html.IndexOf("Root2", StringComparison.Ordinal);
            int child2Idx = html.IndexOf("Child2", StringComparison.Ordinal);

            int totalNodes = tree.Nodes.Count;
            for (int i = 0; i < tree.Nodes.Count; i++) { totalNodes += tree.Nodes[i].ChildNodes.Count; }

            return new object[]
            {
                tree.Nodes.Count,
                html.StartsWith("<div"),
                html.Contains("Root1"),
                html.Contains("Root2"),
                html.Contains("Child2"),
                root2Idx >= 0 && child2Idx > root2Idx,
                root2.ChildNodes.Count,
                totalNodes,
            };
        }

        // Menu with a StaticMenuItemStyle (CssClass + BackColor) and a StaticSelectedStyle applied to
        // the selected item. Verifies the styles reach the rendered output.
        // Returns object[]:
        //   [0] html contains the static item CssClass    -> true
        //   [1] html contains a background-color rule      -> true
        //   [2] html contains the selected item's ForeColor (red)  -> true
        public static object[] MenuStyledItems()
        {
            Menu menu = new Menu();
            menu.StaticMenuItemStyle.CssClass = "staticItem";
            menu.StaticMenuItemStyle.BackColor = System.Drawing.Color.LightGray;
            menu.StaticSelectedStyle.ForeColor = System.Drawing.Color.Red;
            MenuItem file = new MenuItem("File", "f");
            MenuItem edit = new MenuItem("Edit", "e");
            edit.Selected = true;
            menu.Items.Add(file);
            menu.Items.Add(edit);

            Init(menu);
            string html = Render(menu).ToLowerInvariant();

            return new object[]
            {
                html.Contains("staticitem"),
                html.Contains("background-color"),
                html.Contains("color:red") || html.Contains("color: red") || html.Contains("#ff0000") || html.Contains("color:#ff0000"),
            };
        }

        // TreeView with NodeStyle (CssClass) and SelectedNodeStyle (ForeColor) merged onto rendered
        // nodes, plus ShowLines emitting its marker class.
        // Returns object[]:
        //   [0] html contains the node CssClass            -> true
        //   [1] html contains the ShowLines marker class   -> true
        //   [2] html contains the selected node ForeColor  -> true
        public static object[] TreeViewStyledNodes()
        {
            TreeView tree = new TreeView();
            tree.ExpandDepth = 10;
            tree.ShowLines = true;
            tree.NodeStyle.CssClass = "treenode";
            tree.SelectedNodeStyle.ForeColor = System.Drawing.Color.Red;
            TreeNode root1 = new TreeNode("Root1", "r1");
            TreeNode root2 = new TreeNode("Root2", "r2");
            root2.Selected = true;
            tree.Nodes.Add(root1);
            tree.Nodes.Add(root2);

            Init(tree);
            string html = Render(tree).ToLowerInvariant();

            return new object[]
            {
                html.Contains("treenode"),
                html.Contains("treenode_lines"),
                html.Contains("color:red") || html.Contains("color: red") || html.Contains("#ff0000") || html.Contains("color:#ff0000"),
            };
        }
    }
}
