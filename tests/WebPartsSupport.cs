using System;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;

namespace System.Web.Tests
{
    // Support types for the WebParts integration tests. Defined in the test assembly and loaded
    // INTO the ALC by RunInAlc, so their base types (Page / WebPart / WebPartManager / ...) bind
    // to OUR clean-room System.Web.

    // A minimal ITemplate that instantiates a fixed set of children into the zone's container.
    // Mirrors what the ASPX parser would emit for a <ZoneTemplate> with declared parts.
    internal sealed class SimpleZoneTemplate : ITemplate
    {
        private readonly Action<Control> _build;
        public SimpleZoneTemplate(Action<Control> build) { _build = build; }
        public void InstantiateIn(Control container) { _build(container); }
    }

    // A simple WebPart that renders a recognizable body and carries a User-scoped personalizable
    // string property used by the personalization round-trip test.
    internal sealed class PersonalizableWebPart : WebPart
    {
        private string _greeting = "default-greeting";
        private readonly string _body;

        public PersonalizableWebPart() : this("PartBody-A") { }
        public PersonalizableWebPart(string body) { _body = body; }

        [Personalizable(PersonalizationScope.User)]
        public string Greeting
        {
            get { return _greeting; }
            set { _greeting = value; }
        }

        protected internal override void Render(HtmlTextWriter writer)
        {
            writer.Write(_body);
        }
    }

    // The page used by the discovery / render / display-mode / personalization tests:
    //   * one WebPartManager,
    //   * one WebPartZone whose ZoneTemplate yields a PersonalizableWebPart (title "My First Part")
    //     and a Label (title "My Label Part", auto-wrapped into a GenericWebPart),
    //   * one CatalogZone (a ToolZone) used to observe the DisplayMode-driven visibility change.
    internal sealed class WebPartTestPage : Page
    {
        public WebPartManager Manager;
        public WebPartZone Zone;
        public CatalogZone Catalog;
        public PersonalizableWebPart PersonalizablePart;

        protected internal override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            Manager = new WebPartManager();
            Manager.ID = "mgr";

            PersonalizablePart = new PersonalizableWebPart("PartBody-A");
            PersonalizablePart.ID = "part1";
            PersonalizablePart.Title = "My First Part";

            Label label = new Label();
            label.ID = "lbl";
            label.Text = "Hello-Label";

            Zone = new WebPartZone();
            Zone.ID = "zone1";
            Zone.HeaderText = "Main Zone";
            Zone.ZoneTemplate = new SimpleZoneTemplate(delegate (Control c)
            {
                c.Controls.Add(PersonalizablePart);
                GenericWebPart gwp = Manager.CreateWebPart(label);
                gwp.Title = "My Label Part";
                c.Controls.Add(gwp);
            });

            Catalog = new CatalogZone();
            Catalog.ID = "catalog1";

            // The manager must be initialized (registered into Page.Items) before the zones'
            // OnInit runs so they can resolve the current manager.
            Controls.Add(Manager);
            Controls.Add(Zone);
            Controls.Add(Catalog);
        }
    }

    // ---- connection test types ----

    // The connection interface exchanged between provider and consumer.
    internal interface IZipProvider
    {
        string ZipCode { get; }
    }

    // Provider part: exposes its zip via a ConnectionProvider point returning IZipProvider.
    internal sealed class ZipProviderPart : WebPart, IZipProvider
    {
        public string Zip = "98052";
        public string ZipCode { get { return Zip; } }

        [ConnectionProvider("Zip Provider", "ZipProvider")]
        public IZipProvider GetZipProvider() { return this; }

        protected internal override void Render(HtmlTextWriter writer) { writer.Write("provider"); }
    }

    // Consumer part: receives an IZipProvider through a ConnectionConsumer method.
    internal sealed class ZipConsumerPart : WebPart
    {
        public string ReceivedZip;

        [ConnectionConsumer("Zip Consumer", "ZipConsumer")]
        public void SetZipProvider(IZipProvider provider)
        {
            ReceivedZip = provider != null ? provider.ZipCode : null;
        }

        protected internal override void Render(HtmlTextWriter writer) { writer.Write("consumer"); }
    }

    // Page hosting a provider and consumer part inside a single WebPartZone.
    internal sealed class WebPartConnTestPage : Page
    {
        public WebPartManager Manager;
        public WebPartZone Zone;
        public ZipProviderPart Provider;
        public ZipConsumerPart Consumer;

        protected internal override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            Manager = new WebPartManager();
            Manager.ID = "mgr";

            Provider = new ZipProviderPart();
            Provider.ID = "provider";
            Provider.Title = "Provider Part";

            Consumer = new ZipConsumerPart();
            Consumer.ID = "consumer";
            Consumer.Title = "Consumer Part";

            Zone = new WebPartZone();
            Zone.ID = "zone1";
            Zone.ZoneTemplate = new SimpleZoneTemplate(delegate (Control c)
            {
                c.Controls.Add(Provider);
                c.Controls.Add(Consumer);
            });

            Controls.Add(Manager);
            Controls.Add(Zone);
        }
    }
}
