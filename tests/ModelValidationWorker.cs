using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel.DataAnnotations;
using System.Web;
using System.Web.ModelBinding;

namespace System.Web.Tests
{
    // Workers executed INSIDE the custom AssemblyLoadContext so the DefaultModelBinder, the
    // ModelState dictionary and the DataAnnotations validation path all bind to OUR clean-room
    // System.Web. The DefaultModelBinder runs the System.ComponentModel.DataAnnotations
    // ValidationAttributes on the bound model after binding (RunValidation -> RunDataAnnotations),
    // so a model decorated with [Required] / [Range] / [StringLength] populates ModelState with a
    // member error per failed rule. Workers return primitives/strings across the boundary.
    internal static class ModelValidationWorker
    {
        // Same request/context doubles used by ModelBindingWorker, repeated here so this worker is
        // self-contained inside the ALC.
        private sealed class FakeRequest : HttpRequestBase
        {
            private readonly NameValueCollection _form;
            public FakeRequest(NameValueCollection form) { _form = form ?? new NameValueCollection(); }
            public override NameValueCollection Form { get { return _form; } }
            public override NameValueCollection QueryString { get { return new NameValueCollection(); } }
            public override UnvalidatedRequestValuesBase Unvalidated { get { return null; } }
        }

        private sealed class FakeContext : HttpContextBase
        {
            private readonly HttpRequestBase _request;
            public FakeContext(HttpRequestBase request) { _request = request; }
            public override HttpRequestBase Request { get { return _request; } }
        }

        // A model exercising the three canonical DataAnnotations rules called for by the task.
        public sealed class RegistrationModel
        {
            [Required]
            [StringLength(10)]
            public string Name { get; set; }

            [Range(18, 120)]
            public int Age { get; set; }
        }

        private static bool BindRegistration(NameValueCollection form, out ModelStateDictionary modelState, out RegistrationModel model)
        {
            HttpContextBase ctx = new FakeContext(new FakeRequest(form));
            modelState = new ModelStateDictionary();
            ModelBindingExecutionContext ec = new ModelBindingExecutionContext(ctx, modelState);

            ModelBindingContext bindingContext = new ModelBindingContext();
            bindingContext.ModelMetadata = new ModelMetadata(null, null, null, typeof(RegistrationModel), "reg");
            bindingContext.ModelName = "reg";
            bindingContext.ModelState = modelState;
            bindingContext.ValueProvider = new FormValueProvider(ec);

            DefaultModelBinder binder = new DefaultModelBinder();
            bool bound = binder.BindModel(ec, bindingContext);
            model = bindingContext.Model as RegistrationModel;
            return bound;
        }

        private static bool HasError(ModelStateDictionary ms, string key)
        {
            ModelState state;
            return ms.TryGetValue(key, out state) && state.Errors.Count > 0;
        }

        // A fully-valid model produces no validation errors.
        // Returns:
        //   [0] Name bound        (string) "Alice"
        //   [1] Age bound         (int)    30
        //   [2] ModelState valid  (bool)   true
        public static object[] ValidModelPasses()
        {
            NameValueCollection form = new NameValueCollection
            {
                { "reg.Name", "Alice" },
                { "reg.Age", "30" },
            };
            ModelStateDictionary ms;
            RegistrationModel model;
            BindRegistration(form, out ms, out model);
            return new object[]
            {
                model == null ? null : model.Name,
                model == null ? (object)null : model.Age,
                ms.IsValid,
            };
        }

        // Required-rule violation: a missing/empty Name records a member error on reg.Name.
        // Age here is in range and valid, so only the Name member is flagged.
        // Returns:
        //   [0] ModelState invalid     (bool) true (IsValid == false)
        //   [1] error on reg.Name      (bool) true
        //   [2] no error on reg.Age    (bool) true
        public static object[] RequiredViolationReportsMemberError()
        {
            NameValueCollection form = new NameValueCollection
            {
                { "reg.Name", "" },
                { "reg.Age", "25" },
            };
            ModelStateDictionary ms;
            RegistrationModel model;
            BindRegistration(form, out ms, out model);
            return new object[]
            {
                !ms.IsValid,
                HasError(ms, "reg.Name"),
                !HasError(ms, "reg.Age"),
            };
        }

        // Range-rule violation: Age below the [Range(18,120)] minimum records an error on reg.Age.
        // Returns:
        //   [0] ModelState invalid (bool) true
        //   [1] error on reg.Age   (bool) true
        public static object[] RangeViolationReportsMemberError()
        {
            NameValueCollection form = new NameValueCollection
            {
                { "reg.Name", "Bob" },
                { "reg.Age", "5" },
            };
            ModelStateDictionary ms;
            RegistrationModel model;
            BindRegistration(form, out ms, out model);
            return new object[]
            {
                !ms.IsValid,
                HasError(ms, "reg.Age"),
            };
        }

        // StringLength-rule violation: a Name longer than [StringLength(10)] records an error on
        // reg.Name.
        // Returns:
        //   [0] ModelState invalid (bool) true
        //   [1] error on reg.Name  (bool) true
        public static object[] StringLengthViolationReportsMemberError()
        {
            NameValueCollection form = new NameValueCollection
            {
                { "reg.Name", "ThisNameIsFarTooLong" },
                { "reg.Age", "40" },
            };
            ModelStateDictionary ms;
            RegistrationModel model;
            BindRegistration(form, out ms, out model);
            return new object[]
            {
                !ms.IsValid,
                HasError(ms, "reg.Name"),
            };
        }
    }
}
