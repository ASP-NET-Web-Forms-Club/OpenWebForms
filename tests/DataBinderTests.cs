using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier-5b: DataBinder.Eval over a POCO (dotted member path), a DataRowView column, and with a
    // format string. Driven INSIDE the ALC so DataBinder + the System.Data interop bind to OUR
    // clean-room System.Web. Deterministic: invariant numeric format, no clock/culture dependence.
    public class DataBinderTests
    {
        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        [Fact]
        public void EvalResolvesPathsFormatsAndDataRowView()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.DataBindingWorker", "DataBinderEval");

            // Simple property
            Assert.Equal("Ada", (string)r[0]);
            // Dotted nested path Home.City
            Assert.Equal("London", (string)r[1]);
            // Value-type property (boxed int)
            Assert.Equal(36, (int)r[2]);
            // Format string applied (invariant "0.00")
            Assert.Equal("[1234.50]", (string)r[3]);
            // DataRowView column access
            Assert.Equal("Widget", (string)r[4]);
            // DataRowView column with format
            Assert.Equal("Qty: 7", (string)r[5]);
            // Null container short-circuits to null
            Assert.Null(r[6]);
        }
    }
}
