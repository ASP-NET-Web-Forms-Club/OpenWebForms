using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier 3 behavioral tests for System.Web.Security: Forms authentication ticket
    // encrypt/decrypt round-trip + tamper detection, the default in-memory
    // Membership provider, and the default in-memory Role provider.
    //
    // These run INSIDE the ALC (via RunInAlc) so FormsAuthenticationTicket,
    // Membership and Roles all bind to OUR clean-room System.Web.
    public class SecurityTests
    {
        private static SystemWebUnderTest SW => SystemWebUnderTest.Instance;

        [Fact]
        public void FormsAuth_Encrypt_Decrypt_RoundTrips_AndDetectsTampering()
        {
            // [0] name matches, [1] userData matches, [2] expiry-within-tolerance,
            // [3] tampered cookie rejected (null)
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.SecurityWorker", "FormsTicketRoundTrip");
            Assert.Equal(true, r[0]);
            Assert.Equal(true, r[1]);
            Assert.Equal(true, r[2]);
            Assert.Equal(true, r[3]);
        }

        [Fact]
        public void Membership_CreateUser_Validate_ChangePassword()
        {
            // [0] validate-correct true, [1] validate-wrong false,
            // [2] change-password true, [3] validate-old-after-change false,
            // [4] validate-new-after-change true
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.SecurityWorker", "MembershipLifecycle");
            Assert.Equal(true, r[0]);
            Assert.Equal(false, r[1]);
            Assert.Equal(true, r[2]);
            Assert.Equal(false, r[3]);
            Assert.Equal(true, r[4]);
        }

        [Fact]
        public void Roles_CreateRole_AddUser_IsUserInRole()
        {
            // [0] role exists after create, [1] member in role true,
            // [2] non-member in role false, [3] removed member false
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.SecurityWorker", "RolesLifecycle");
            Assert.Equal(true, r[0]);
            Assert.Equal(true, r[1]);
            Assert.Equal(false, r[2]);
            Assert.Equal(false, r[3]);
        }
    }

    public static class SecurityWorker
    {
        public static object[] FormsTicketRoundTrip()
        {
            DateTime issue = DateTime.Now;
            DateTime expire = issue.AddMinutes(30);
            global::System.Web.Security.FormsAuthenticationTicket ticket =
                new global::System.Web.Security.FormsAuthenticationTicket(
                    2, "alice", issue, expire, false, "role=admin|dept=eng", "/");

            string encrypted = global::System.Web.Security.FormsAuthentication.Encrypt(ticket);
            global::System.Web.Security.FormsAuthenticationTicket decoded =
                global::System.Web.Security.FormsAuthentication.Decrypt(encrypted);

            bool nameOk = decoded != null && decoded.Name == "alice";
            bool userDataOk = decoded != null && decoded.UserData == "role=admin|dept=eng";
            // Expiration round-trips to within a second (UTC ticks serialized exactly).
            bool expiryOk = decoded != null &&
                Math.Abs((decoded.Expiration - expire).TotalSeconds) < 1.0;

            // Tamper with the encrypted hex by flipping the final byte's low nibble.
            char[] chars = encrypted.ToCharArray();
            int last = chars.Length - 1;
            char c = chars[last];
            chars[last] = (c == '0') ? '1' : '0';
            string tampered = new string(chars);
            global::System.Web.Security.FormsAuthenticationTicket tamperedTicket =
                global::System.Web.Security.FormsAuthentication.Decrypt(tampered);
            bool tamperRejected = tamperedTicket == null;

            return new object[] { nameOk, userDataOk, expiryOk, tamperRejected };
        }

        public static object[] MembershipLifecycle()
        {
            // Unique username so repeated runs in the same process do not collide.
            string user = "muser_" + Guid.NewGuid().ToString("N");
            const string pwd = "Passw0rd!";
            const string newPwd = "N3wPassw0rd!";

            global::System.Web.Security.Membership.CreateUser(user, pwd, user + "@example.com");

            bool validCorrect = global::System.Web.Security.Membership.ValidateUser(user, pwd);
            bool validWrong = global::System.Web.Security.Membership.ValidateUser(user, "wrong-password");

            bool changed = global::System.Web.Security.Membership.Provider.ChangePassword(user, pwd, newPwd);

            bool oldFails = global::System.Web.Security.Membership.ValidateUser(user, pwd);
            bool newWorks = global::System.Web.Security.Membership.ValidateUser(user, newPwd);

            return new object[] { validCorrect, validWrong, changed, oldFails, newWorks };
        }

        public static object[] RolesLifecycle()
        {
            string role = "role_" + Guid.NewGuid().ToString("N");
            string member = "ruser_" + Guid.NewGuid().ToString("N");
            string outsider = "ruser_" + Guid.NewGuid().ToString("N");

            global::System.Web.Security.Roles.CreateRole(role);
            bool roleExists = global::System.Web.Security.Roles.RoleExists(role);

            global::System.Web.Security.Roles.AddUserToRole(member, role);
            bool memberIn = global::System.Web.Security.Roles.IsUserInRole(member, role);
            bool outsiderIn = global::System.Web.Security.Roles.IsUserInRole(outsider, role);

            global::System.Web.Security.Roles.RemoveUserFromRole(member, role);
            bool removedIn = global::System.Web.Security.Roles.IsUserInRole(member, role);

            return new object[] { roleExists, memberIn, outsiderIn, removedIn };
        }
    }
}
