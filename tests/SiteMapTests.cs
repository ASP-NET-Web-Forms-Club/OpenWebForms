using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier-7: System.Web SiteMap end-to-end, driven INSIDE the ALC. Deterministic and cross-platform
    // (the Web.sitemap is written to a unique temp file via Path.GetTempPath, see SiteMapWorker).
    //
    //   * An XmlSiteMapProvider loads a small Web.sitemap -> RootNode + child nodes (Title/Url).
    //   * The provider's CurrentNode resolves for a request path that matches a node URL, and the
    //     resolved node exposes its parent.
    //   * A SiteMapPath control bound to the provider renders the breadcrumb trail
    //     (Home > Products > Details) now that SiteMap is implemented.
    public class SiteMapTests
    {
        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        [Fact]
        public void XmlSiteMapProviderLoadsRootAndChildren()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.SiteMapWorker", "LoadRootAndChildren");

            Assert.True((bool)r[0], "RootNode should load from the temp Web.sitemap");
            Assert.Equal("Home", (string)r[1]);
            Assert.Equal("/Default.aspx", (string)r[2]);
            Assert.Equal(2, (int)r[3]);              // Products + About
            Assert.Equal("Products", (string)r[4]);
            Assert.Equal("/Products.aspx", (string)r[5]);
            Assert.Equal("Details", (string)r[6]);   // grandchild under Products
        }

        [Fact]
        public void CurrentNodeResolvesForRequestPath()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.SiteMapWorker", "CurrentNodeResolvesForPath");

            Assert.True((bool)r[0], "CurrentNode should resolve for /Products.aspx");
            Assert.Equal("Products", (string)r[1]);
            Assert.Equal("/Products.aspx", (string)r[2]);
            Assert.Equal("Home", (string)r[3]);      // parent of Products is Home
            Assert.Equal("About", (string)r[4]);     // FindSiteMapNode by url
        }

        [Fact]
        public void SiteMapPathRendersBreadcrumbTrail()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.SiteMapWorker", "SiteMapPathRendersBreadcrumb");

            string html = (string)r[0];
            Assert.True((bool)r[1], "breadcrumb should contain root 'Home': " + html);
            Assert.True((bool)r[2], "breadcrumb should contain 'Products': " + html);
            Assert.True((bool)r[3], "breadcrumb should contain current 'Details': " + html);
            Assert.Equal(2, (int)r[4]);              // two separators between three nodes
            Assert.True((bool)r[5], "nodes should render Home -> Products -> Details in order: " + html);
        }
    }
}
