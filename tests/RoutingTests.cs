using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier-7: System.Web.Routing end-to-end, driven INSIDE the ALC. Deterministic and cross-platform
    // (no real HttpContext / filesystem state -- routing is exercised through hand-written
    // HttpContextBase / HttpRequestBase doubles, see RoutingWorker).
    //
    //   * A Route "products/{category}/{id}" + defaults matches a path and surfaces the segment
    //     values (category, id) plus a default value not present in the URL.
    //   * A regex constraint rejects a value that fails the pattern.
    //   * An HttpMethodConstraint rejects a disallowed verb.
    //   * GetVirtualPath rebuilds the URL back from a value dictionary.
    //   * RouteCollection.MapPageRoute installs a PageRouteHandler-backed route; GetRouteData
    //     resolves it and the handler exposes the page virtual path.
    public class RoutingTests
    {
        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        [Fact]
        public void RouteMatchesPathAndSurfacesValuesWithDefaults()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.RoutingWorker", "MatchWithDefaults");

            Assert.True((bool)r[0], "route should match ~/products/electronics/42");
            Assert.Equal("electronics", (string)r[1]);
            Assert.Equal("42", (string)r[2]);
            Assert.Equal("browse", (string)r[3]);    // default applied for the absent 'action' value
            Assert.True((bool)r[4], "a non-matching path should produce null RouteData");
        }

        [Fact]
        public void RegexConstraintRejectsBadValue()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.RoutingWorker", "ConstraintRejectsBadValue");

            Assert.True((bool)r[0], "numeric id should satisfy the \\d+ constraint");
            Assert.True((bool)r[1], "non-numeric id should be rejected by the \\d+ constraint");
        }

        [Fact]
        public void HttpMethodConstraintRejectsDisallowedVerb()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.RoutingWorker", "HttpMethodConstraintRejectsVerb");

            Assert.True((bool)r[0], "GET should be allowed by the HttpMethodConstraint");
            Assert.True((bool)r[1], "POST should be rejected by the HttpMethodConstraint");
        }

        [Fact]
        public void GetVirtualPathBuildsUrlFromValues()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.RoutingWorker", "GetVirtualPathBuildsUrl");

            Assert.True((bool)r[0], "GetVirtualPath should produce a VirtualPathData");
            // Route.GetVirtualPath (called directly, not via the collection) returns the
            // app-relative form; the RouteCollection is what normalizes "~/" to the app path.
            Assert.Equal("~/products/electronics/42", (string)r[1]);
        }

        [Fact]
        public void MapPageRouteResolvesToPageRouteHandler()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.RoutingWorker", "MapPageRouteResolvesHandler");

            Assert.True((bool)r[0], "MapPageRoute'd route should match ~/products/99");
            Assert.True((bool)r[1], "matched route's handler should be a PageRouteHandler");
            Assert.Equal("~/ProductDetails.aspx", (string)r[2]);
            Assert.Equal("99", (string)r[3]);
            Assert.True((bool)r[4], "route should be retrievable by name from the collection");
        }
    }
}
