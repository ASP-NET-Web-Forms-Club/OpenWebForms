using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier 9 (System.Web.ModelBinding) integration tests. Each scenario runs inside the
    // custom AssemblyLoadContext (via RunInAlc) so the value providers, DefaultModelBinder,
    // ModelState, ModelBindingExecutionContext and the ModelDataSource pipeline all bind to
    // OUR clean-room System.Web. Workers return primitives/strings (BCL types shared across
    // load contexts) back across the boundary for assertion, keeping the tests deterministic
    // and cross-platform.
    public class ModelBindingTests
    {
        private static SystemWebUnderTest SW => SystemWebUnderTest.Instance;

        [Fact]
        public void FormAndQueryStringProviders_ReturnResultsForKeys()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.ModelBindingWorker", "FormAndQueryStringProviders");
            Assert.Equal("Alice", r[0]);   // form["name"]
            Assert.True((bool)r[1]);        // ContainsPrefix("name")
            Assert.True((bool)r[2]);        // missing form key -> null
            Assert.Equal("42", r[3]);       // query["id"]
            Assert.True((bool)r[4]);        // form provider does not see query key
        }

        [Fact]
        public void ValueProviderCollection_CompositesAcrossProviders()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.ModelBindingWorker", "ValueProviderCollectionComposite");
            Assert.Equal("42", r[0]);        // from query-only provider
            Assert.Equal("Alice", r[1]);     // from form-only provider
            Assert.Equal("form-dup", r[2]);  // first provider (form) wins shared key
            Assert.True((bool)r[3]);          // absent key -> null
            Assert.True((bool)r[4]);          // ContainsPrefix union
        }

        [Fact]
        public void DefaultModelBinder_BindsSimpleTypes()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.ModelBindingWorker", "DefaultBinderSimpleTypes");
            Assert.Equal(42, r[0]);     // int parsed from "42"
            Assert.True((bool)r[1]);     // bind succeeded
            Assert.Equal("Alice", r[2]); // string bound
            Assert.True((bool)r[3]);     // ModelState valid
        }

        [Fact]
        public void DefaultModelBinder_InvalidConversion_AddsModelError()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.ModelBindingWorker", "DefaultBinderInvalidConversion");
            Assert.True((bool)r[0]);   // bind returned false
            Assert.False((bool)r[1]);  // ModelState IsValid == false
            Assert.True((bool)r[2]);   // a ModelError was recorded for "age"
        }

        [Fact]
        public void DefaultModelBinder_BindsComplexPocoFromPrefixedForm()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.ModelBindingWorker", "DefaultBinderComplexPoco");
            Assert.Equal("Bob", r[0]);  // person.Name
            Assert.Equal(37, r[1]);     // person.Age
            Assert.True((bool)r[2]);     // bound
            Assert.True((bool)r[3]);     // IsValid
        }

        [Fact]
        public void DefaultModelBinder_ComplexPoco_InvalidProperty_RecordsError()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.ModelBindingWorker", "DefaultBinderComplexPocoInvalid");
            Assert.Equal("Carol", r[0]); // valid property still bound
            Assert.False((bool)r[1]);    // IsValid == false
            Assert.True((bool)r[2]);     // error recorded on person.Age
        }

        [Fact]
        public void ModelDataSource_SelectMethod_ResolvesQueryStringParameter()
        {
            object[] r = (object[])SW.RunInAlc(
                "System.Web.Tests.ModelBindingWorker", "ModelDataSourceSelectWithQueryString");
            Assert.Equal("tools", r[0]); // category fed to the select method
            Assert.Equal(3, r[1]);       // rows returned for category=tools
            Assert.Equal("Widget", r[2]); // first row
        }
    }
}
