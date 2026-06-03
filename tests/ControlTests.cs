using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier-4 Control-cluster gate. Drives ControlWorker INSIDE the ALC (so Control binds to
    // OUR clean-room System.Web) and asserts on the returned facts:
    //   * mutated child view state survives a save -> fresh-tree -> load round-trip;
    //   * FindControl resolves ids within their naming container (and only there);
    //   * recursive RenderControl produces the expected nested HTML via HtmlTextWriter.
    // Deterministic and cross-platform: no machineKey, no clock, no culture-sensitive output.
    public class ControlTests
    {
        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        [Fact]
        public void ControlTreeRoundTripFindAndRender()
        {
            object[] r = (object[])Web.RunInAlc(
                "System.Web.Tests.ControlWorker", "Run");

            string roundTripAlpha = (string)r[0];
            string roundTripBeta = (string)r[1];
            string foundDirectId = (string)r[2];
            bool foundNestedNull = (bool)r[3];
            string foundQualified = (string)r[4];
            string renderHtml = (string)r[5];

            // View state round-trip: the mutated child values survived save/load across a
            // freshly rebuilt, identically shaped tree.
            Assert.Equal("ALPHA-CHANGED", roundTripAlpha);
            Assert.Equal("BETA-CHANGED", roundTripBeta);

            // FindControl: direct id resolves inside its own naming container.
            Assert.Equal("alpha", foundDirectId);

            // FindControl: an id that lives in a nested naming container is NOT visible from
            // the outer container.
            Assert.True(foundNestedNull, "FindControl should not cross into a child naming container");

            // FindControl: but it IS resolvable from its own naming container.
            Assert.Equal("beta", foundQualified);

            // Recursive render walks the whole tree through HtmlTextWriter and reflects the
            // restored (mutated) view state.
            Assert.Equal(
                "<div id=\"root\"><span>ALPHA-CHANGED</span><section><span>BETA-CHANGED</span></section></div>",
                renderHtml);
        }
    }
}
