using System;
using System.IO;
using System.Reflection;
using System.Web.UI;

namespace System.Web.Tests
{
    // Worker executed INSIDE the custom AssemblyLoadContext (via RunInAlc), so the base
    // type Control binds to OUR clean-room System.Web rather than the shared-framework
    // facade. Exercises the Control cluster end to end:
    //   * builds a control tree (root naming container -> two children, one nested);
    //   * tracks + saves view state, mutates child state, reloads into a fresh identical
    //     tree, and confirms the mutated child state survives the round-trip;
    //   * FindControl by id (direct + qualified path through naming containers);
    //   * recursive RenderControl into an HtmlTextWriter and compares to expected HTML.
    //
    // SaveViewStateRecursive / LoadViewStateRecursive are `internal` to System.Web, so the
    // worker (in the test assembly) reaches them by reflection.
    internal static class ControlWorker
    {
        private static readonly BindingFlags Inst =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static object SaveTree(Control root)
        {
            MethodInfo mi = typeof(Control).GetMethod("SaveViewStateRecursive", Inst);
            return mi.Invoke(root, Array.Empty<object>());
        }

        private static void LoadTree(Control root, object state)
        {
            MethodInfo mi = typeof(Control).GetMethod("LoadViewStateRecursive", Inst);
            mi.Invoke(root, new object[] { state });
        }

        // Drive the whole tree through Init so every control's StateBag begins tracking
        // deltas (TrackViewState is per-control; InitRecursive cascades it like the page
        // lifecycle does). Without this, child mutations are never recorded as dirty.
        private static void Track(Control root)
        {
            MethodInfo mi = typeof(Control).GetMethod("InitRecursive", Inst);
            mi.Invoke(root, new object[] { null });
        }

        // Returns object[]:
        //   [0] roundTripAlpha (string)    -- TextLeaf "alpha" value restored on the fresh tree
        //   [1] roundTripBeta (string)     -- nested leaf "beta" value restored on the fresh tree
        //   [2] foundDirectId (string)     -- FindControl("alpha").ID on root
        //   [3] foundNestedNull (bool)     -- FindControl("beta") at root is null (different container)
        //   [4] foundQualified (string)    -- group.FindControl("beta").ID
        //   [5] renderHtml (string)        -- recursive render of the whole tree
        public static object[] Run()
        {
            // ---- build authoring tree ----
            RootContainer root = BuildTree();
            Track(root);

            // mutate child view state AFTER tracking so the deltas are recorded
            TextLeaf alpha = (TextLeaf)root.FindControl("alpha");
            GroupContainer group = (GroupContainer)root.FindControl("group");
            TextLeaf beta = (TextLeaf)group.FindControl("beta");
            alpha.Text = "ALPHA-CHANGED";
            beta.Text = "BETA-CHANGED";

            object saved = SaveTree(root);

            // ---- fresh identical tree, load saved state ----
            RootContainer root2 = BuildTree();
            Track(root2);
            LoadTree(root2, saved);

            TextLeaf alpha2 = (TextLeaf)root2.FindControl("alpha");
            GroupContainer group2 = (GroupContainer)root2.FindControl("group");
            TextLeaf beta2 = (TextLeaf)group2.FindControl("beta");

            // ---- FindControl semantics ----
            // "beta" lives inside the "group" naming container, so it is NOT visible from root.
            Control betaFromRoot = root2.FindControl("beta");
            Control betaQualified = group2.FindControl("beta");

            // ---- recursive render ----
            StringWriter sw = new StringWriter();
            HtmlTextWriter w = new HtmlTextWriter(sw);
            root2.RenderControl(w);
            w.Flush();
            string html = sw.ToString();

            return new object[]
            {
                alpha2.Text,
                beta2.Text,
                alpha2.ID,
                betaFromRoot == null,
                betaQualified != null ? betaQualified.ID : null,
                html,
            };
        }

        private static RootContainer BuildTree()
        {
            RootContainer root = new RootContainer();
            root.ID = "root";

            TextLeaf alpha = new TextLeaf();
            alpha.ID = "alpha";
            alpha.Text = "alpha-initial";
            root.Controls.Add(alpha);

            GroupContainer group = new GroupContainer();
            group.ID = "group";
            root.Controls.Add(group);

            TextLeaf beta = new TextLeaf();
            beta.ID = "beta";
            beta.Text = "beta-initial";
            group.Controls.Add(beta);

            return root;
        }
    }

    // Root naming container; renders a <div> wrapper around its children.
    internal sealed class RootContainer : Control, INamingContainer
    {
        protected internal override void Render(HtmlTextWriter writer)
        {
            writer.Write("<div id=\"root\">");
            RenderChildren(writer);
            writer.Write("</div>");
        }
    }

    // A nested naming container so qualified FindControl paths and per-container id
    // scoping can be exercised.
    internal sealed class GroupContainer : Control, INamingContainer
    {
        protected internal override void Render(HtmlTextWriter writer)
        {
            writer.Write("<section>");
            RenderChildren(writer);
            writer.Write("</section>");
        }
    }

    // Leaf control whose Text is persisted in view state and emitted on render.
    internal sealed class TextLeaf : Control
    {
        public string Text
        {
            get { object o = ViewState["Text"]; return o == null ? string.Empty : (string)o; }
            set { ViewState["Text"] = value; }
        }

        protected internal override void Render(HtmlTextWriter writer)
        {
            writer.Write("<span>" + Text + "</span>");
        }
    }
}
