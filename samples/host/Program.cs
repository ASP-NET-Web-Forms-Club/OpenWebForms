// Sample standalone host driving the internal System.Web HTTP server via InternalsVisibleTo.
// NO ASP.NET Core anywhere.
//
// This Main is a thin trampoline running in the DEFAULT AssemblyLoadContext. It does NOT touch any
// System.Web type directly (that would bind to the shared-framework throw-only facade on the TPA
// list). Instead it hands off to AlcBootstrap, which creates a custom AssemblyLoadContext that
// routes the "System.Web" simple name to our app-local System.Web.dll, loads this host assembly
// into that context, and invokes RealEntry.Run by reflection. From there, all serving code runs
// inside the custom ALC and binds to OUR clean-room System.Web.
//
// At Tier 0 each request yields HTTP 500 (HttpRuntime.ProcessRequest throws NotImplementedException,
// which the server catches) -- that is expected and proves OUR runtime executed.
using System;

namespace SampleHost
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            return AlcBootstrap.Bootstrap(args);
        }
    }
}
