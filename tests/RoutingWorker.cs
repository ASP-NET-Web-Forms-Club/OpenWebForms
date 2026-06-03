using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Routing;

namespace System.Web.Tests
{
    // Workers executed INSIDE the custom AssemblyLoadContext (via RunInAlc) so that the routing
    // types (Route, RouteCollection, RouteData, PageRouteHandler, ...) and the HttpContextBase /
    // HttpRequestBase test doubles all bind to OUR clean-room System.Web rather than the
    // shared-framework facade.
    //
    // The routing surface is driven through HttpContextBase, so the workers supply lightweight
    // hand-written doubles (FakeRoutingRequest / FakeRoutingContext) that override only the members
    // the routing engine consults: AppRelativeCurrentExecutionFilePath, PathInfo, ApplicationPath,
    // HttpMethod, plus Request on the context. This keeps the tests deterministic and free of any
    // real HttpWorkerRequest / HttpContext.Current state.
    internal static class RoutingWorker
    {
        // A minimal HttpRequestBase double. Only the members routing reads are overridden.
        private sealed class FakeRoutingRequest : HttpRequestBase
        {
            private readonly string _appRelativePath;
            private readonly string _pathInfo;
            private readonly string _httpMethod;
            public FakeRoutingRequest(string appRelativePath, string pathInfo, string httpMethod)
            {
                _appRelativePath = appRelativePath;
                _pathInfo = pathInfo ?? string.Empty;
                _httpMethod = httpMethod ?? "GET";
            }
            public override string AppRelativeCurrentExecutionFilePath { get { return _appRelativePath; } }
            public override string PathInfo { get { return _pathInfo; } }
            public override string ApplicationPath { get { return "/"; } }
            public override string HttpMethod { get { return _httpMethod; } }
        }

        private sealed class FakeRoutingContext : HttpContextBase
        {
            private readonly HttpRequestBase _request;
            public FakeRoutingContext(HttpRequestBase request) { _request = request; }
            public override HttpRequestBase Request { get { return _request; } }
        }

        private static HttpContextBase Context(string appRelativePath, string httpMethod)
        {
            return new FakeRoutingContext(new FakeRoutingRequest(appRelativePath, string.Empty, httpMethod));
        }

        // Route "products/{category}/{id}" + defaults matches "~/products/electronics/42".
        // Returns object[]:
        //   [0] routeData != null         (bool)   -- the route matched
        //   [1] category value            (string) -- "electronics"
        //   [2] id value                  (string) -- "42"
        //   [3] action default applied    (string) -- "browse" (default not present in URL)
        //   [4] non-matching path -> null (bool)   -- "~/orders/1" does not match
        public static object[] MatchWithDefaults()
        {
            RouteValueDictionary defaults = new RouteValueDictionary();
            defaults.Add("action", "browse");
            Route route = new Route("products/{category}/{id}", defaults, new StubRouteHandler());

            RouteData rd = route.GetRouteData(Context("~/products/electronics/42", "GET"));

            string category = null, id = null, action = null;
            if (rd != null)
            {
                object v;
                if (rd.Values.TryGetValue("category", out v)) { category = Convert.ToString(v); }
                if (rd.Values.TryGetValue("id", out v)) { id = Convert.ToString(v); }
                if (rd.Values.TryGetValue("action", out v)) { action = Convert.ToString(v); }
            }

            RouteData noMatch = route.GetRouteData(Context("~/orders/1", "GET"));

            return new object[]
            {
                rd != null,
                category,
                id,
                action,
                noMatch == null,
            };
        }

        // A regex constraint {id => \d+} rejects a bad value.
        // Returns object[]:
        //   [0] good "~/products/books/42" matches (bool)
        //   [1] bad  "~/products/books/abc" rejected -> null (bool)
        public static object[] ConstraintRejectsBadValue()
        {
            RouteValueDictionary constraints = new RouteValueDictionary();
            constraints.Add("id", @"\d+");
            Route route = new Route("products/{category}/{id}", null, constraints, new StubRouteHandler());

            RouteData good = route.GetRouteData(Context("~/products/books/42", "GET"));
            RouteData bad = route.GetRouteData(Context("~/products/books/abc", "GET"));

            return new object[]
            {
                good != null,
                bad == null,
            };
        }

        // An HttpMethodConstraint rejects a disallowed verb on an incoming request.
        // Returns object[]:
        //   [0] GET allowed -> matches (bool)
        //   [1] POST rejected -> null  (bool)
        public static object[] HttpMethodConstraintRejectsVerb()
        {
            RouteValueDictionary constraints = new RouteValueDictionary();
            constraints.Add("httpMethod", new HttpMethodConstraint("GET", "HEAD"));
            Route route = new Route("products/{category}/{id}", null, constraints, new StubRouteHandler());

            RouteData get = route.GetRouteData(Context("~/products/books/1", "GET"));
            RouteData post = route.GetRouteData(Context("~/products/books/1", "POST"));

            return new object[]
            {
                get != null,
                post == null,
            };
        }

        // GetVirtualPath rebuilds the URL from values (round-trips the URL pattern).
        // Returns object[]:
        //   [0] vpd != null                 (bool)
        //   [1] the produced virtual path   (string) -- "/products/electronics/42"
        public static object[] GetVirtualPathBuildsUrl()
        {
            Route route = new Route("products/{category}/{id}", new StubRouteHandler());

            // Establish a current request context (empty current values) for URL generation.
            RouteData current = new RouteData();
            RequestContext requestContext = new RequestContext(Context("~/", "GET"), current);

            RouteValueDictionary values = new RouteValueDictionary();
            values.Add("category", "electronics");
            values.Add("id", "42");

            VirtualPathData vpd = route.GetVirtualPath(requestContext, values);

            return new object[]
            {
                vpd != null,
                vpd == null ? null : vpd.VirtualPath,
            };
        }

        // RouteCollection.MapPageRoute installs a Route whose handler is a PageRouteHandler that
        // resolves to the configured page virtual path; GetRouteData via the collection returns the
        // matched RouteData and the handler exposes the page path.
        // Returns object[]:
        //   [0] routeData != null                          (bool)
        //   [1] handler is PageRouteHandler                 (bool)
        //   [2] PageRouteHandler.VirtualPath               (string) -- "~/ProductDetails.aspx"
        //   [3] id route value from the matched route      (string) -- "99"
        //   [4] route retrievable by name from collection  (bool)
        public static object[] MapPageRouteResolvesHandler()
        {
            RouteCollection routes = new RouteCollection();
            routes.MapPageRoute("productDetail", "products/{id}", "~/ProductDetails.aspx");

            RouteData rd = routes.GetRouteData(Context("~/products/99", "GET"));

            bool isPageHandler = false;
            string pagePath = null;
            string id = null;
            if (rd != null)
            {
                PageRouteHandler prh = rd.RouteHandler as PageRouteHandler;
                isPageHandler = prh != null;
                if (prh != null) { pagePath = prh.VirtualPath; }
                object v;
                if (rd.Values.TryGetValue("id", out v)) { id = Convert.ToString(v); }
            }

            bool byName = routes["productDetail"] != null;

            return new object[]
            {
                rd != null,
                isPageHandler,
                pagePath,
                id,
                byName,
            };
        }

        // A trivial IRouteHandler that resolves to no real handler; used where the test only needs a
        // route to carry/return a handler reference (not execute it).
        private sealed class StubRouteHandler : IRouteHandler
        {
            public IHttpHandler GetHttpHandler(RequestContext requestContext) { return null; }
        }
    }
}
