using System;
using System.Collections;
using System.Collections.Specialized;
using System.Reflection;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace System.Web.Tests
{
    // Workers executed INSIDE the custom AssemblyLoadContext (via RunInAlc) so MailDefinition,
    // System.Web.Mail.MailMessage, and the System.Net.Mail target types bind to OUR clean-room
    // System.Web rather than the shared-framework facade.
    //
    // Covers the "MailSecCore" cluster:
    //   * MailDefinition.CreateMailMessage builds a System.Net.Mail.MailMessage with case-insensitive
    //     {token} replacement applied to Subject and Body, From/To/CC wired, Priority/IsBodyHtml honored.
    //   * Legacy System.Web.Mail.MailMessage maps to a System.Net.Mail.MailMessage (ToNetMailMessage),
    //     translating From/To/Cc/Bcc, BodyFormat -> IsBodyHtml, and MailPriority across the two enums.
    internal static class MailWorker
    {
        private static readonly BindingFlags Inst =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        // ---- A: MailDefinition.CreateMailMessage with token replacement ----
        // Returns object[]:
        //   [0] subject after replacement   -> "Welcome Ada"
        //   [1] body after replacement      -> "Hello Ada, your code is 1234."
        //   [2] From address                -> "noreply@site.test"
        //   [3] To count                    -> 1
        //   [4] To[0]                        -> "ada@user.test"
        //   [5] CC count                    -> 2 (CC = "a@x.test, b@y.test")
        //   [6] IsBodyHtml                   -> true
        //   [7] Priority == High            -> true
        public static object[] MailDefinitionTokenReplacement()
        {
            MailDefinition def = new MailDefinition();
            def.From = "noreply@site.test";
            def.Subject = "Welcome <% Name %>";   // placeholder text; tokens below are literal {..}
            def.CC = "a@x.test, b@y.test";
            def.IsBodyHtml = true;
            def.Priority = System.Net.Mail.MailPriority.High;

            // Use literal {token} markers; replacements are case-insensitive on the key text.
            def.Subject = "Welcome <%name%>";

            ListDictionary repl = new ListDictionary();
            repl["<%name%>"] = "Ada";
            repl["<%CODE%>"] = "1234";

            string body = "Hello <%Name%>, your code is <%code%>.";

            System.Net.Mail.MailMessage msg = def.CreateMailMessage("ada@user.test", repl, body, null);

            return new object[]
            {
                msg.Subject,
                msg.Body,
                msg.From != null ? msg.From.Address : null,
                msg.To.Count,
                msg.To.Count > 0 ? msg.To[0].Address : null,
                msg.CC.Count,
                msg.IsBodyHtml,
                msg.Priority == System.Net.Mail.MailPriority.High,
            };
        }

        // ---- B: legacy System.Web.Mail.MailMessage -> System.Net.Mail.MailMessage ----
        // ToNetMailMessage() is internal, so it is reached by reflection.
        // Returns object[]:
        //   [0] From address         -> "from@legacy.test"
        //   [1] To count             -> 2 (legacy ';' separated)
        //   [2] CC count             -> 1
        //   [3] Bcc count            -> 1
        //   [4] Subject              -> "Legacy Subject"
        //   [5] Body                 -> "Legacy Body"
        //   [6] IsBodyHtml           -> true (BodyFormat = Html)
        //   [7] Priority == Low      -> true (Web.Mail.MailPriority.Low -> Net.Mail.MailPriority.Low)
        public static object[] LegacyMailMessageMapsToNet()
        {
            global::System.Web.Mail.MailMessage legacy = new global::System.Web.Mail.MailMessage();
            legacy.From = "from@legacy.test";
            legacy.To = "to1@legacy.test;to2@legacy.test";
            legacy.Cc = "cc@legacy.test";
            legacy.Bcc = "bcc@legacy.test";
            legacy.Subject = "Legacy Subject";
            legacy.Body = "Legacy Body";
            legacy.BodyFormat = global::System.Web.Mail.MailFormat.Html;
            legacy.Priority = global::System.Web.Mail.MailPriority.Low;

            MethodInfo mi = legacy.GetType().GetMethod("ToNetMailMessage", Inst);
            System.Net.Mail.MailMessage net = (System.Net.Mail.MailMessage)mi.Invoke(legacy, Array.Empty<object>());

            return new object[]
            {
                net.From != null ? net.From.Address : null,
                net.To.Count,
                net.CC.Count,
                net.Bcc.Count,
                net.Subject,
                net.Body,
                net.IsBodyHtml,
                net.Priority == System.Net.Mail.MailPriority.Low,
            };
        }
    }
}
