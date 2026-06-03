using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier 3 behavioral tests for System.Web.Profile: the default in-memory
    // ProfileProvider's SettingsProvider round-trip, and the ProfileBase indexer
    // Save/reload path through the process-wide provider store.
    //
    // Run INSIDE the ALC so SettingsContext/SettingsProperty and our Profile types
    // bind consistently to OUR clean-room System.Web.
    public class ProfileTests
    {
        private static SystemWebUnderTest SW => SystemWebUnderTest.Instance;

        [Fact]
        public void DefaultProfileProvider_SetThenGet_RoundTrips()
        {
            // [0] retrieved value equals stored value
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.ProfileWorker", "ProviderRoundTrip");
            Assert.Equal("dark-blue", r[0]);
        }

        [Fact]
        public void ProfileBase_Indexer_Save_Reload_PersistsValue()
        {
            // [0] reloaded value equals the saved value
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.ProfileWorker", "ProfileBaseSaveReload");
            Assert.Equal("Helvetica", r[0]);
        }
    }

    public static class ProfileWorker
    {
        public static object[] ProviderRoundTrip()
        {
            string user = "puser_" + Guid.NewGuid().ToString("N");

            global::System.Web.Profile.DefaultProfileProvider provider =
                new global::System.Web.Profile.DefaultProfileProvider();
            provider.Initialize("TestProvider", new global::System.Collections.Specialized.NameValueCollection());

            // Settings context identifies the user / authentication state.
            global::System.Configuration.SettingsContext ctx = new global::System.Configuration.SettingsContext();
            ctx["UserName"] = user;
            ctx["IsAuthenticated"] = true;

            // Describe one string property and give it a value.
            global::System.Configuration.SettingsProperty prop =
                new global::System.Configuration.SettingsProperty("Theme");
            prop.PropertyType = typeof(string);

            global::System.Configuration.SettingsPropertyCollection propsToRead =
                new global::System.Configuration.SettingsPropertyCollection();
            propsToRead.Add(prop);

            global::System.Configuration.SettingsPropertyValue toSet =
                new global::System.Configuration.SettingsPropertyValue(prop);
            toSet.PropertyValue = "dark-blue";

            global::System.Configuration.SettingsPropertyValueCollection setColl =
                new global::System.Configuration.SettingsPropertyValueCollection();
            setColl.Add(toSet);

            provider.SetPropertyValues(ctx, setColl);

            // Read it back via the provider.
            global::System.Configuration.SettingsPropertyValueCollection got =
                provider.GetPropertyValues(ctx, propsToRead);
            global::System.Configuration.SettingsPropertyValue back = got["Theme"];
            object value = back == null ? null : back.PropertyValue;

            return new object[] { value as string };
        }

        public static object[] ProfileBaseSaveReload()
        {
            string user = "pbuser_" + Guid.NewGuid().ToString("N");

            // First instance: set a property via the indexer and persist it.
            global::System.Web.Profile.ProfileBase profile =
                global::System.Web.Profile.ProfileBase.Create(user, true);
            profile["FontFamily"] = "Helvetica";
            profile.Save();

            // Fresh instance for the same user reloads from the process-wide provider.
            global::System.Web.Profile.ProfileBase reloaded =
                global::System.Web.Profile.ProfileBase.Create(user, true);
            object value = reloaded["FontFamily"];

            return new object[] { value as string };
        }
    }
}
