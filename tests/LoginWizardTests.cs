using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier-5b-2ii: MultiView / Wizard / Login, driven INSIDE the ALC. Deterministic.
    //   * MultiView.ActiveViewIndex shows only the active View.
    //   * Wizard navigates Start -> Step -> Finish showing the active step.
    //   * Login.OnAuthenticate against a Tier-3 in-memory Membership user.
    public class LoginWizardTests
    {
        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        [Fact]
        public void MultiViewShowsOnlyActiveView()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.LoginWizardWorker", "MultiViewActiveView");

            Assert.Equal(3, (int)r[0]);                 // three views
            Assert.True((bool)r[1], "active view (index 1) content 'VIEW-B' should render");
            Assert.False((bool)r[2], "inactive view 'VIEW-A' should NOT render");
            Assert.False((bool)r[3], "inactive view 'VIEW-C' should NOT render");
            Assert.Equal(1, (int)r[4]);                 // ActiveViewIndex
            Assert.True((bool)r[5], "GetActiveView() should be Views[1]");
        }

        [Fact]
        public void WizardNavigatesStartStepFinish()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.LoginWizardWorker", "WizardNavigation");

            Assert.True((bool)r[0], "start step body should render");
            Assert.True((bool)r[1], "start step should not show the middle step's body");
            Assert.True((bool)r[2], "start step should render a 'Next' button");
            Assert.True((bool)r[3], "after MoveNext the middle step body should render");
            Assert.True((bool)r[4], "middle step should render a 'Previous' button");
            Assert.True((bool)r[5], "after MoveTo(finish) the finish step body should render");
            Assert.True((bool)r[6], "finish step should render a 'Finish' button");
            Assert.Equal(3, (int)r[7]);                 // three wizard steps
        }

        [Fact]
        public void LoginAuthenticatesAgainstInMemoryMembership()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.LoginWizardWorker", "LoginAuthenticate");

            Assert.True((bool)r[0], "valid credentials should authenticate via the default path");
            Assert.False((bool)r[1], "bad password should NOT authenticate via the default path");
            Assert.True((bool)r[2], "custom Authenticate handler should be invoked and honoured");
            Assert.True((bool)r[3], "a failed AttemptLogin should raise LoginError");
            Assert.True((bool)r[4], "Membership.ValidateUser should confirm the seeded user");
        }

        [Fact]
        public void CustomTemplatesReplaceDefaultLayout()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.LoginWizardWorker", "CustomTemplates");

            Assert.True((bool)r[0], "Login LayoutTemplate marker should render");
            Assert.True((bool)r[1], "Login default layout (UserNameLabelText) should be suppressed by the template");
            Assert.True((bool)r[2], "Wizard HeaderTemplate marker should render");
        }
    }
}
