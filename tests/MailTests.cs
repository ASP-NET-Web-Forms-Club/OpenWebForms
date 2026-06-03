using Xunit;

namespace System.Web.Tests
{
    // Tier-8 "MailSecCore" cluster, driven INSIDE the ALC so MailDefinition / System.Web.Mail map
    // onto System.Net.Mail through OUR clean-room System.Web.
    //
    //   * MailDefinition.CreateMailMessage performs case-insensitive {token} replacement on Subject
    //     and Body and wires From/To/CC, IsBodyHtml and Priority onto a System.Net.Mail.MailMessage.
    //   * The legacy System.Web.Mail.MailMessage maps onto System.Net.Mail (From/To/Cc/Bcc, the
    //     BodyFormat->IsBodyHtml flag, and the MailPriority enum translation).
    public class MailTests
    {
        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        [Fact]
        public void MailDefinitionCreateMailMessageReplacesTokens()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.MailWorker", "MailDefinitionTokenReplacement");

            Assert.Equal("Welcome Ada", (string)r[0]);
            Assert.Equal("Hello Ada, your code is 1234.", (string)r[1]);
            Assert.Equal("noreply@site.test", (string)r[2]);
            Assert.Equal(1, (int)r[3]);
            Assert.Equal("ada@user.test", (string)r[4]);
            Assert.Equal(2, (int)r[5]);
            Assert.True((bool)r[6], "IsBodyHtml should be honored");
            Assert.True((bool)r[7], "Priority High should be honored");
        }

        [Fact]
        public void LegacyMailMessageMapsToSystemNetMail()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.MailWorker", "LegacyMailMessageMapsToNet");

            Assert.Equal("from@legacy.test", (string)r[0]);
            Assert.Equal(2, (int)r[1]);   // ';'-separated To -> two recipients
            Assert.Equal(1, (int)r[2]);
            Assert.Equal(1, (int)r[3]);
            Assert.Equal("Legacy Subject", (string)r[4]);
            Assert.Equal("Legacy Body", (string)r[5]);
            Assert.True((bool)r[6], "BodyFormat.Html -> IsBodyHtml");
            Assert.True((bool)r[7], "Web.Mail Low priority -> Net.Mail Low priority");
        }
    }
}
