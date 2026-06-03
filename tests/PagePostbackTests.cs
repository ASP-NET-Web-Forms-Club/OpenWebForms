using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier-4 Page postback round-trip gate. Drives a hand-written Page subclass through the
    // full lifecycle inside the ALC (so Page/Control bind to OUR clean-room System.Web):
    //   GET renders __VIEWSTATE; POST replays it + __EVENTTARGET; control state restores,
    //   the postback event raises and mutates the counter, posted form data loads into the
    //   textbox, and the re-saved __VIEWSTATE reflects the mutated state.
    public class PagePostbackTests
    {
        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        [Fact]
        public void PostbackRoundTrip()
        {
            object[] r = (object[])Web.RunInAlc(
                "System.Web.Tests.PageWorker", "RoundTrip");

            string getBody = (string)r[0];
            string getViewState = (string)r[1];
            string postViewState = (string)r[2];
            int counterAfterPost = (int)r[3];
            int counterRestoredOnPost = (int)r[4];
            string textboxValueOnPost = (string)r[5];
            bool dataChangedFired = (bool)r[6];
            bool eventRaised = (bool)r[7];

            // GET emitted a __VIEWSTATE hidden field.
            Assert.Contains("name=\"__VIEWSTATE\"", getBody);
            Assert.False(string.IsNullOrEmpty(getViewState), "GET produced an empty __VIEWSTATE");

            // POST restored control state (counter started at 0 from GET save).
            Assert.Equal(0, counterRestoredOnPost);

            // The postback event fired and incremented the counter.
            Assert.True(eventRaised, "RaisePostBackEvent did not fire");
            Assert.Equal(1, counterAfterPost);

            // Posted form data flowed into the textbox via IPostBackDataHandler.
            Assert.Equal("typed-value", textboxValueOnPost);
            Assert.True(dataChangedFired, "RaisePostDataChangedEvent did not fire");

            // The re-saved __VIEWSTATE differs from the GET one (mutated state).
            Assert.False(string.IsNullOrEmpty(postViewState), "POST produced an empty __VIEWSTATE");
            Assert.NotEqual(getViewState, postViewState);
        }
    }
}
