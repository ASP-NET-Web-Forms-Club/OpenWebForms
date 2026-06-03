using System;
using System.IO;
using System.Reflection;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace System.Web.Tests
{
    // Workers executed INSIDE the custom AssemblyLoadContext (via RunInAlc) so MultiView / Wizard /
    // Login bind to OUR clean-room System.Web rather than the shared-framework facade.
    //
    // Covers:
    //   * MultiView.ActiveViewIndex renders ONLY the active View's content.
    //   * Wizard navigates Start -> Step -> Finish (via ActiveStepIndex / MoveTo) and renders the
    //     active step's content plus the matching navigation buttons.
    //   * Login.OnAuthenticate validates against the Tier-3 in-memory Membership provider:
    //       - default (no handler) path: valid creds authenticate, bad creds do not.
    //       - custom Authenticate handler path: the handler controls the outcome.
    //       - AttemptLogin with bad creds raises LoginError.
    internal static class LoginWizardWorker
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

        private static void Invoke(object instance, string method, params object[] args)
        {
            MethodInfo mi = null;
            Type t = instance.GetType();
            while (t != null && mi == null)
            {
                mi = t.GetMethod(method, Inst, null, ArgTypes(args), null);
                t = t.BaseType;
            }
            if (mi == null) { throw new MissingMethodException(instance.GetType().FullName + "." + method); }
            mi.Invoke(instance, args);
        }

        private static Type[] ArgTypes(object[] args)
        {
            Type[] r = new Type[args.Length];
            for (int i = 0; i < args.Length; i++) { r[i] = args[i].GetType(); }
            return r;
        }

        // ===================== MultiView =====================

        // Three Views each containing a distinct literal. With ActiveViewIndex=1 only the second
        // view's content renders.
        // Returns object[]:
        //   [0] Views.Count               -> 3
        //   [1] html contains "VIEW-B"    (active)
        //   [2] html contains "VIEW-A"    -> false (inactive)
        //   [3] html contains "VIEW-C"    -> false (inactive)
        //   [4] ActiveViewIndex           -> 1
        //   [5] GetActiveView() is Views[1]
        public static object[] MultiViewActiveView()
        {
            MultiView mv = new MultiView();
            View a = new View(); a.Controls.Add(new LiteralControl("VIEW-A"));
            View b = new View(); b.Controls.Add(new LiteralControl("VIEW-B"));
            View c = new View(); c.Controls.Add(new LiteralControl("VIEW-C"));
            mv.Controls.Add(a);
            mv.Controls.Add(b);
            mv.Controls.Add(c);
            mv.ActiveViewIndex = 1;

            Init(mv);
            string html = Render(mv);

            return new object[]
            {
                mv.Views.Count,
                html.Contains("VIEW-B"),
                html.Contains("VIEW-A"),
                html.Contains("VIEW-C"),
                mv.ActiveViewIndex,
                object.ReferenceEquals(mv.GetActiveView(), b),
            };
        }

        // ===================== Wizard =====================

        private static WizardStep MakeStep(string id, string body, WizardStepType type)
        {
            WizardStep s = new WizardStep();
            s.ID = id;
            s.StepType = type;
            s.Controls.Add(new LiteralControl(body));
            return s;
        }

        // Wizard with Start / Step / Finish steps. Navigate forward and confirm the active step's
        // content renders (and only that step's content), plus the appropriate nav button text.
        // Returns object[]:
        //   [0] start: contains "START-BODY"
        //   [1] start: NOT "STEP-BODY"
        //   [2] start: contains "Next" button text (StartNextButtonText)
        //   [3] after MoveNext -> ActiveStepIndex == 1, contains "STEP-BODY", not "START-BODY"
        //   [4] step: contains "Previous" (step has a previous button)
        //   [5] after MoveTo(finish) -> ActiveStepIndex == 2, contains "FINISH-BODY"
        //   [6] finish: contains "Finish" button text (FinishCompleteButtonText)
        //   [7] WizardSteps.Count -> 3
        public static object[] WizardNavigation()
        {
            Wizard wiz = new Wizard();
            wiz.DisplaySideBar = false; // keep output focused on the active step + nav
            WizardStep start = MakeStep("Start", "START-BODY", WizardStepType.Start);
            WizardStep step = MakeStep("Step", "STEP-BODY", WizardStepType.Step);
            WizardStep finish = MakeStep("Finish", "FINISH-BODY", WizardStepType.Finish);
            wiz.WizardSteps.Add(start);
            wiz.WizardSteps.Add(step);
            wiz.WizardSteps.Add(finish);

            Init(wiz);

            // Start step (index defaults to 0).
            string startHtml = Render(wiz);
            bool startBody = startHtml.Contains("START-BODY");
            bool startNoStep = !startHtml.Contains("STEP-BODY");
            bool startNext = startHtml.Contains("Next");

            // Move to the middle Step.
            wiz.ActiveStepIndex = 1;
            string stepHtml = Render(wiz);
            int stepIndex = wiz.ActiveStepIndex;
            bool stepBody = stepHtml.Contains("STEP-BODY") && !stepHtml.Contains("START-BODY");
            bool stepPrev = stepHtml.Contains("Previous");

            // Move to the Finish step via MoveTo.
            wiz.MoveTo(finish);
            string finishHtml = Render(wiz);
            int finishIndex = wiz.ActiveStepIndex;
            bool finishBody = finishHtml.Contains("FINISH-BODY");
            bool finishButton = finishHtml.Contains("Finish");

            return new object[]
            {
                startBody,
                startNoStep,
                startNext,
                stepIndex == 1 && stepBody,
                stepPrev,
                finishIndex == 2 && finishBody,
                finishButton,
                wiz.WizardSteps.Count,
            };
        }

        // ===================== Login =====================

        // Sets the Login's user-name / password child text boxes, then invokes OnAuthenticate.
        private static void SetCredentials(Login login, string user, string password)
        {
            login.UserName = user; // setter writes into the UserName text box
            TextBox pwd = login.FindControl("Password") as TextBox;
            if (pwd != null) { pwd.Text = password; }
        }

        // Login.OnAuthenticate against the Tier-3 in-memory Membership provider.
        // Creates a user, then exercises valid + invalid credentials through the default
        // (handler-less) OnAuthenticate path, a custom Authenticate handler, and the LoginError event.
        // Returns object[]:
        //   [0] valid creds (default path)   -> Authenticated == true
        //   [1] bad password (default path)  -> Authenticated == false
        //   [2] custom Authenticate handler invoked AND its result honoured (true)
        //   [3] LoginError fired on a failed AttemptLogin (bad creds, no handler)
        //   [4] Membership.ValidateUser confirms the seeded user directly
        public static object[] LoginAuthenticate()
        {
            string user = "wizuser_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string password = "P@ssw0rd!23";
            Membership.CreateUser(user, password);
            bool membershipValid = Membership.ValidateUser(user, password);

            // --- default (handler-less) path: valid creds ---
            Login login1 = new Login();
            Init(login1);
            SetCredentials(login1, user, password);
            AuthenticateEventArgs ok = new AuthenticateEventArgs();
            Invoke(login1, "OnAuthenticate", ok);
            bool validDefault = ok.Authenticated;

            // --- default path: bad password ---
            Login login2 = new Login();
            Init(login2);
            SetCredentials(login2, user, "wrong-password");
            AuthenticateEventArgs bad = new AuthenticateEventArgs();
            Invoke(login2, "OnAuthenticate", bad);
            bool badDefault = bad.Authenticated;

            // --- custom Authenticate handler path ---
            Login login3 = new Login();
            bool handlerCalled = false;
            login3.Authenticate += (sender, e) =>
            {
                handlerCalled = true;
                e.Authenticated = Membership.ValidateUser(((Login)sender).UserName, ((Login)sender).Password);
            };
            Init(login3);
            SetCredentials(login3, user, password);
            AuthenticateEventArgs custom = new AuthenticateEventArgs();
            Invoke(login3, "OnAuthenticate", custom);
            bool customOk = handlerCalled && custom.Authenticated;

            // --- LoginError event on a failed AttemptLogin (FailureAction defaults to Refresh, so no
            // redirect is attempted; with no Page the success path's RedirectFromLoginPage is avoided
            // by using bad credentials) ---
            Login login4 = new Login();
            bool loginErrorFired = false;
            login4.LoginError += (sender, e) => { loginErrorFired = true; };
            Init(login4);
            SetCredentials(login4, user, "still-wrong");
            Invoke(login4, "AttemptLogin");

            return new object[]
            {
                validDefault,
                badDefault,
                customOk,
                loginErrorFired,
                membershipValid,
            };
        }

        // A trivial template that drops a marker literal (and an optional named TextBox) into its
        // container, used to prove custom *Template instantiation replaces the default layout.
        private sealed class MarkerTemplate : ITemplate
        {
            private readonly string _marker;
            private readonly string _textBoxId;
            public MarkerTemplate(string marker, string textBoxId) { _marker = marker; _textBoxId = textBoxId; }
            public void InstantiateIn(Control container)
            {
                container.Controls.Add(new LiteralControl(_marker));
                if (_textBoxId != null)
                {
                    TextBox tb = new TextBox(); tb.ID = _textBoxId; container.Controls.Add(tb);
                }
            }
        }

        // ===================== Custom templates: Login + Wizard =====================
        // Returns object[]:
        //   [0] login html contains custom marker          -> true
        //   [1] login html omits default UserNameLabelText -> true (default layout suppressed)
        //   [2] wizard html contains header marker         -> true
        public static object[] CustomTemplates()
        {
            Login login = new Login();
            login.UserNameLabelText = "DEFAULT-USERNAME-LABEL";
            login.LayoutTemplate = new MarkerTemplate("LOGIN-CUSTOM-LAYOUT", "UserName");
            Init(login);
            string loginHtml = Render(login);

            Wizard wiz = new Wizard();
            WizardStep s1 = new WizardStep(); s1.Controls.Add(new LiteralControl("STEP-ONE")); wiz.WizardSteps.Add(s1);
            wiz.HeaderTemplate = new MarkerTemplate("WIZARD-CUSTOM-HEADER", null);
            wiz.ActiveStepIndex = 0;
            Init(wiz);
            string wizHtml = Render(wiz);

            return new object[]
            {
                loginHtml.Contains("LOGIN-CUSTOM-LAYOUT"),
                !loginHtml.Contains("DEFAULT-USERNAME-LABEL"),
                wizHtml.Contains("WIZARD-CUSTOM-HEADER"),
            };
        }
    }
}
