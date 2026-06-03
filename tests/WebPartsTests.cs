using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier-5c: the WebParts subsystem, driven INSIDE the ALC so the controls bind to OUR
    // clean-room System.Web. Deterministic and cross-platform (no HTTP transport / no DB).
    //
    //   * A WebPartManager + a WebPartZone whose ZoneTemplate yields two parts (a custom WebPart
    //     and a Label auto-wrapped in a GenericWebPart). The manager discovers the parts and the
    //     zone renders them with title-bar chrome; the part titles appear in the HTML.
    //   * Switching WebPartManager.DisplayMode to the catalog mode flips a CatalogZone (a ToolZone)
    //     from not-displayed to displayed.
    //   * A personalizable property round-trips through the default personalization provider:
    //     extracted+saved from one tree, loaded+applied to a fresh tree.
    //   * A provider/consumer WebPartConnection activates and the consumer receives the provider
    //     interface data.
    public class WebPartsTests
    {
        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        [Fact]
        public void ManagerDiscoversPartsAndZoneRendersChromeWithTitles()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.WebPartsWorker", "DiscoverAndRender");

            Assert.Equal(2, (int)r[0]);                 // manager discovered both parts
            Assert.Equal(2, (int)r[1]);                 // zone exposes both parts
            Assert.True((bool)r[2], "first part title 'My First Part' should render in chrome");
            Assert.True((bool)r[3], "label part title 'My Label Part' should render in chrome");
            Assert.True((bool)r[4], "zone/chrome should render a <table>");
            Assert.True((bool)r[5], "the custom part body 'PartBody-A' should render");
            Assert.True((bool)r[6], "the wrapped Label's text 'Hello-Label' should render");
            Assert.True((bool)r[7], "GetCurrentWebPartManager should return the page's manager");
        }

        [Fact]
        public void DisplayModeSwitchChangesToolZoneVisibility()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.WebPartsWorker", "DisplayModeSwitch");

            Assert.Equal("Browse", (string)r[0]);       // default display mode
            Assert.False((bool)r[1], "CatalogZone must NOT display in Browse mode");
            Assert.True((bool)r[2], "CatalogZone MUST display once the manager is in Catalog mode");
            Assert.True((bool)r[3], "DisplayModeChanged should fire on the switch");
            Assert.Equal("Catalog", (string)r[4]);      // new display mode
            Assert.True((bool)r[5], "the ordinary WebPartZone stays visible in both modes");
        }

        [Fact]
        public void PersonalizablePropertyRoundTripsThroughDefaultProvider()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.WebPartsWorker", "PersonalizationRoundTrip");

            Assert.Equal("PERSISTED-VALUE-42", (string)r[0]);   // value set on the source part
            Assert.Equal("default-greeting", (string)r[1]);     // fresh part starts at its default
            Assert.Equal("PERSISTED-VALUE-42", (string)r[2]);   // fresh part adopts the saved value
            Assert.True((bool)r[3], "personalization should round-trip via the provider");
            Assert.Equal(2, (int)r[4]);                          // both parts were under the manager
        }

        [Fact]
        public void WebPartConnectionActivatesAndConsumerReceivesProviderData()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.WebPartsWorker", "ConnectionActivates");

            Assert.True((bool)r[0], "provider connection point should be discovered");
            Assert.True((bool)r[1], "consumer connection point should be discovered");
            Assert.True((bool)r[2], "ConnectWebParts should create a connection");
            Assert.True((bool)r[3], "the connection should report active after activation");
            Assert.Equal((string)r[5], (string)r[4]);   // consumer received the provider's zip
            Assert.Equal("98052", (string)r[4]);
        }
    }
}
