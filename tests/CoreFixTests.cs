using Xunit;

namespace System.Web.Tests
{
    // Tier-8 core loose-ends, driven INSIDE the ALC against OUR clean-room System.Web.
    //
    //   * Control.EnsureID assigns a stable automatic "ctlNN" id once a control is parented into a
    //     naming container, and the id does not change on repeated EnsureID calls.
    //   * The Page __VIEWSTATE MAC pipeline round-trips and honors a configured MachineKeySection
    //     validationKey (the configured key bytes drive the HMAC; tampering is rejected).
    public class CoreFixTests
    {
        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        [Fact]
        public void EnsureIdAssignsStableAutoId()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.CoreFixWorker", "EnsureIdAssignsStableAutoId");

            Assert.Equal("ctl00", (string)r[0]);
            Assert.Equal("ctl01", (string)r[1]);
            Assert.True((bool)r[2], "auto-id must be stable across repeated EnsureID calls");
            Assert.True((bool)r[3], "UniqueID should incorporate the generated id");
            Assert.Equal("myId", (string)r[4]);   // explicit id is preserved
        }

        [Fact]
        public void PageMacHonorsConfiguredMachineKey()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.CoreFixWorker", "PageMacHonorsConfiguredKey");

            Assert.True((bool)r[0], "Page ApplyViewStateMac/StripViewStateMac should round-trip");
            Assert.True((bool)r[1], "GetMacKeyBytes(configured ValidationKey) should equal HexToBytes(key)");
            Assert.True((bool)r[2], "configured key should produce a different HMAC than an unrelated key");
            Assert.True((bool)r[3], "recomputing under the configured key should reproduce the MAC");
            Assert.True((bool)r[4], "a tampered __VIEWSTATE MAC should be rejected");
        }
    }
}
