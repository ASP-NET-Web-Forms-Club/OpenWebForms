using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier 10 model-validation integration tests. A model decorated with the canonical
    // DataAnnotations rules ([Required] / [Range] / [StringLength]) is bound through the
    // DefaultModelBinder; after binding the DataAnnotations validators run and populate
    // ModelState with a member error per failed rule. Valid input leaves ModelState valid;
    // each invalid input flags exactly the expected member. Runs inside the custom
    // AssemblyLoadContext so the binder and ModelState bind to OUR clean-room System.Web.
    public class ModelValidationTests
    {
        private static SystemWebUnderTest SW => SystemWebUnderTest.Instance;

        [Fact]
        public void ValidModel_BindsAndPassesValidation()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.ModelValidationWorker", "ValidModelPasses");
            Assert.Equal("Alice", r[0]);
            Assert.Equal(30, r[1]);
            Assert.True((bool)r[2]); // ModelState.IsValid
        }

        [Fact]
        public void RequiredViolation_ReportsErrorOnNameMemberOnly()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.ModelValidationWorker", "RequiredViolationReportsMemberError");
            Assert.True((bool)r[0]);  // IsValid == false
            Assert.True((bool)r[1]);  // error on reg.Name
            Assert.True((bool)r[2]);  // no error on reg.Age
        }

        [Fact]
        public void RangeViolation_ReportsErrorOnAgeMember()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.ModelValidationWorker", "RangeViolationReportsMemberError");
            Assert.True((bool)r[0]); // IsValid == false
            Assert.True((bool)r[1]); // error on reg.Age
        }

        [Fact]
        public void StringLengthViolation_ReportsErrorOnNameMember()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.ModelValidationWorker", "StringLengthViolationReportsMemberError");
            Assert.True((bool)r[0]); // IsValid == false
            Assert.True((bool)r[1]); // error on reg.Name
        }
    }
}
