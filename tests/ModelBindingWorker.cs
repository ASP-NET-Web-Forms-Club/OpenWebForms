using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Web;
using System.Web.ModelBinding;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace System.Web.Tests
{
    // Workers executed INSIDE the custom AssemblyLoadContext (via RunInAlc) so that the
    // System.Web.ModelBinding types (value providers, DefaultModelBinder, ModelState, the
    // ModelBindingExecutionContext) and the HttpContextBase / HttpRequestBase doubles all
    // bind to OUR clean-room System.Web rather than the shared-framework facade.
    //
    // The value-provider / binder scenarios drive everything through HttpRequestBase by
    // supplying a tiny hand-written request double (FakeBindingRequest) that returns
    // in-memory Form / QueryString collections. The ModelDataSource scenario additionally
    // needs HttpContext.Current (the view builds an HttpContextWrapper around it) plus a
    // data-methods object supplied through the CallingDataMethods event.
    internal static class ModelBindingWorker
    {
        // ----- request / context doubles -------------------------------------------------

        private sealed class FakeBindingRequest : HttpRequestBase
        {
            private readonly NameValueCollection _form;
            private readonly NameValueCollection _queryString;

            public FakeBindingRequest(NameValueCollection form, NameValueCollection queryString)
            {
                _form = form ?? new NameValueCollection();
                _queryString = queryString ?? new NameValueCollection();
            }

            public override NameValueCollection Form { get { return _form; } }
            public override NameValueCollection QueryString { get { return _queryString; } }
            // Returning null exercises the providers' documented fall-back to the validated
            // collection when no Unvalidated surface is present.
            public override UnvalidatedRequestValuesBase Unvalidated { get { return null; } }
        }

        private sealed class FakeBindingContext : HttpContextBase
        {
            private readonly HttpRequestBase _request;
            public FakeBindingContext(HttpRequestBase request) { _request = request; }
            public override HttpRequestBase Request { get { return _request; } }
        }

        private static ModelBindingExecutionContext MakeExecutionContext(
            NameValueCollection form, NameValueCollection queryString, out ModelStateDictionary modelState)
        {
            HttpContextBase ctx = new FakeBindingContext(new FakeBindingRequest(form, queryString));
            modelState = new ModelStateDictionary();
            return new ModelBindingExecutionContext(ctx, modelState);
        }

        private static ModelMetadata MetadataFor(Type modelType, string name)
        {
            return new ModelMetadata(null, null, null, modelType, name);
        }

        // ----- value provider scenarios --------------------------------------------------

        // FormValueProvider returns a ValueProviderResult for a present key and null for an
        // absent one; QueryStringValueProvider likewise over the query string.
        // Returns:
        //   [0] form value for "name"           (string) "Alice"
        //   [1] form provider ContainsPrefix     (bool)   true for "name"
        //   [2] form provider for missing key    (bool)   true == null result
        //   [3] query value for "id"             (string) "42"
        //   [4] form provider does not see query (bool)   true == null (isolation)
        public static object[] FormAndQueryStringProviders()
        {
            NameValueCollection form = new NameValueCollection { { "name", "Alice" } };
            NameValueCollection query = new NameValueCollection { { "id", "42" } };
            ModelStateDictionary ms;
            ModelBindingExecutionContext ec = MakeExecutionContext(form, query, out ms);

            FormValueProvider formProvider = new FormValueProvider(ec);
            QueryStringValueProvider queryProvider = new QueryStringValueProvider(ec);

            ValueProviderResult nameResult = formProvider.GetValue("name");
            ValueProviderResult missingResult = formProvider.GetValue("absent");
            ValueProviderResult idResult = queryProvider.GetValue("id");
            ValueProviderResult crossResult = formProvider.GetValue("id");

            return new object[]
            {
                nameResult == null ? null : nameResult.AttemptedValue,
                formProvider.ContainsPrefix("name"),
                missingResult == null,
                idResult == null ? null : idResult.AttemptedValue,
                crossResult == null,
            };
        }

        // ValueProviderCollection composites across multiple providers: a key present in the
        // second provider only is still found; first match wins for a shared key; absent ->
        // null; ContainsPrefix is the union.
        // Returns:
        //   [0] value of "id" (query only)   (string) "42"
        //   [1] value of "name" (form only)  (string) "Alice"
        //   [2] shared "dup" first-wins      (string) "form-dup"
        //   [3] absent key -> null           (bool)   true
        //   [4] ContainsPrefix union "id"    (bool)   true
        public static object[] ValueProviderCollectionComposite()
        {
            NameValueCollection form = new NameValueCollection { { "name", "Alice" }, { "dup", "form-dup" } };
            NameValueCollection query = new NameValueCollection { { "id", "42" }, { "dup", "query-dup" } };
            ModelStateDictionary ms;
            ModelBindingExecutionContext ec = MakeExecutionContext(form, query, out ms);

            ValueProviderCollection collection = new ValueProviderCollection();
            collection.Add(new FormValueProvider(ec));
            collection.Add(new QueryStringValueProvider(ec));

            ValueProviderResult id = collection.GetValue("id");
            ValueProviderResult name = collection.GetValue("name");
            ValueProviderResult dup = collection.GetValue("dup");
            ValueProviderResult absent = collection.GetValue("absent");

            return new object[]
            {
                id == null ? null : id.AttemptedValue,
                name == null ? null : name.AttemptedValue,
                dup == null ? null : dup.AttemptedValue,
                absent == null,
                collection.ContainsPrefix("id"),
            };
        }

        // ----- DefaultModelBinder: simple types ------------------------------------------

        // Binds an int and a string from a value provider.
        // Returns:
        //   [0] bound int          (int)    42
        //   [1] int bind succeeded (bool)   true
        //   [2] bound string       (string) "Alice"
        //   [3] ModelState valid   (bool)   true
        public static object[] DefaultBinderSimpleTypes()
        {
            NameValueCollection form = new NameValueCollection { { "age", "42" }, { "name", "Alice" } };
            ModelStateDictionary ms;
            ModelBindingExecutionContext ec = MakeExecutionContext(form, null, out ms);
            IValueProvider provider = new FormValueProvider(ec);

            ModelBindingContext intCtx = new ModelBindingContext();
            intCtx.ModelMetadata = MetadataFor(typeof(int), "age");
            intCtx.ModelName = "age";
            intCtx.ModelState = ms;
            intCtx.ValueProvider = provider;
            DefaultModelBinder binder = new DefaultModelBinder();
            bool intBound = binder.BindModel(ec, intCtx);
            object intModel = intCtx.Model;

            ModelBindingContext strCtx = new ModelBindingContext();
            strCtx.ModelMetadata = MetadataFor(typeof(string), "name");
            strCtx.ModelName = "name";
            strCtx.ModelState = ms;
            strCtx.ValueProvider = provider;
            bool strBound = binder.BindModel(ec, strCtx);
            object strModel = strCtx.Model;

            return new object[]
            {
                intModel,
                intBound,
                strModel,
                ms.IsValid,
            };
        }

        // Binding an int from a non-numeric form value records a ModelError and IsValid=false.
        // Returns:
        //   [0] bind returned false      (bool) true
        //   [1] ModelState IsValid       (bool) false
        //   [2] error recorded for "age" (bool) true
        public static object[] DefaultBinderInvalidConversion()
        {
            NameValueCollection form = new NameValueCollection { { "age", "not-a-number" } };
            ModelStateDictionary ms;
            ModelBindingExecutionContext ec = MakeExecutionContext(form, null, out ms);

            ModelBindingContext ctx = new ModelBindingContext();
            ctx.ModelMetadata = MetadataFor(typeof(int), "age");
            ctx.ModelName = "age";
            ctx.ModelState = ms;
            ctx.ValueProvider = new FormValueProvider(ec);
            DefaultModelBinder binder = new DefaultModelBinder();
            bool bound = binder.BindModel(ec, ctx);

            ModelState state;
            bool hasError = ms.TryGetValue("age", out state) && state.Errors.Count > 0;

            return new object[]
            {
                !bound,
                ms.IsValid,
                hasError,
            };
        }

        // ----- DefaultModelBinder: complex POCO ------------------------------------------

        // Person bound from prefixed form values "person.Name" / "person.Age".
        public sealed class Person
        {
            public string Name { get; set; }
            public int Age { get; set; }
        }

        // Returns:
        //   [0] Name    (string) "Bob"
        //   [1] Age     (int)    37
        //   [2] bound   (bool)   true
        //   [3] IsValid (bool)   true
        public static object[] DefaultBinderComplexPoco()
        {
            NameValueCollection form = new NameValueCollection
            {
                { "person.Name", "Bob" },
                { "person.Age", "37" },
            };
            ModelStateDictionary ms;
            ModelBindingExecutionContext ec = MakeExecutionContext(form, null, out ms);

            ModelBindingContext ctx = new ModelBindingContext();
            ctx.ModelMetadata = MetadataFor(typeof(Person), "person");
            ctx.ModelName = "person";
            ctx.ModelState = ms;
            ctx.ValueProvider = new FormValueProvider(ec);
            DefaultModelBinder binder = new DefaultModelBinder();
            bool bound = binder.BindModel(ec, ctx);

            Person p = ctx.Model as Person;
            return new object[]
            {
                p == null ? null : p.Name,
                p == null ? (object)null : p.Age,
                bound,
                ms.IsValid,
            };
        }

        // A complex POCO where one property has an invalid conversion: the valid property is
        // still bound, an error is recorded for the bad property, and IsValid is false.
        // Returns:
        //   [0] Name bound          (string) "Carol"
        //   [1] IsValid             (bool)   false
        //   [2] error on person.Age (bool)   true
        public static object[] DefaultBinderComplexPocoInvalid()
        {
            NameValueCollection form = new NameValueCollection
            {
                { "person.Name", "Carol" },
                { "person.Age", "xyz" },
            };
            ModelStateDictionary ms;
            ModelBindingExecutionContext ec = MakeExecutionContext(form, null, out ms);

            ModelBindingContext ctx = new ModelBindingContext();
            ctx.ModelMetadata = MetadataFor(typeof(Person), "person");
            ctx.ModelName = "person";
            ctx.ModelState = ms;
            ctx.ValueProvider = new FormValueProvider(ec);
            DefaultModelBinder binder = new DefaultModelBinder();
            binder.BindModel(ec, ctx);

            Person p = ctx.Model as Person;
            ModelState ageState;
            bool ageError = ms.TryGetValue("person.Age", out ageState) && ageState.Errors.Count > 0;

            return new object[]
            {
                p == null ? null : p.Name,
                ms.IsValid,
                ageError,
            };
        }

        // ----- ModelDataSource: SelectMethod with [QueryString] parameter ----------------

        public sealed class Product
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        // The data-methods object whose SelectMethod resolves its parameter from the query
        // string via the [QueryString] attribute.
        public sealed class ProductRepository
        {
            public List<Product> GetProducts([QueryString] string category)
            {
                List<Product> all = new List<Product>
                {
                    new Product { Id = 1, Name = "Widget" },
                    new Product { Id = 2, Name = "Gadget" },
                    new Product { Id = 3, Name = "Sprocket" },
                };
                List<Product> matches = new List<Product>();
                foreach (Product p in all)
                {
                    if (string.Equals(category, "tools", StringComparison.OrdinalIgnoreCase))
                    {
                        matches.Add(p);
                    }
                }
                return matches;
            }
        }

        // A trivial Control subclass to act as the ModelDataSource's data control.
        private sealed class ProductsHostControl : Control
        {
        }

        // A ModelDataSource that hands out a test view. The base view's private GetHttpContext()
        // wraps the ambient HttpContext in an HttpContextWrapper (a Tier-2 glue type that is not
        // part of the model-binding cluster and is still unimplemented). To keep this test inside
        // the model-binding surface, the view below overrides the protected EvaluateMethod
        // pipeline to build a ModelBindingExecutionContext over a plain HttpContextBase double and
        // resolve each parameter through the REAL value-provider attribute + DefaultModelBinder.
        // Everything else (FindMethod, InvokeMethod, ProcessSelectMethodResult, CreateSelectResult)
        // runs the genuine ModelDataSource code.
        private sealed class TestModelDataSource : ModelDataSource
        {
            private readonly HttpContextBase _httpContext;
            public TestModelDataSource(Control dataControl, HttpContextBase httpContext)
                : base(dataControl)
            {
                _httpContext = httpContext;
            }
            public override ModelDataSourceView View
            {
                get { return ViewInstance ?? (ViewInstance = new TestModelDataSourceView(this, _httpContext)); }
            }
            private TestModelDataSourceView ViewInstance;
        }

        private sealed class TestModelDataSourceView : ModelDataSourceView
        {
            private readonly HttpContextBase _httpContext;
            public TestModelDataSourceView(ModelDataSource owner, HttpContextBase httpContext)
                : base(owner)
            {
                _httpContext = httpContext;
            }

            protected override void EvaluateMethodParameters(
                DataSourceOperation dataSourceOperation,
                ModelDataSourceMethod modelDataSourceMethod,
                IDictionary controlValues,
                bool isPageLoadComplete)
            {
                if (modelDataSourceMethod == null || modelDataSourceMethod.MethodInfo == null) { return; }
                ModelStateDictionary modelState = new ModelStateDictionary();
                ModelBindingExecutionContext ec = new ModelBindingExecutionContext(_httpContext, modelState);
                System.Reflection.ParameterInfo[] parameters = modelDataSourceMethod.MethodInfo.GetParameters();
                for (int i = 0; i < parameters.Length; i++)
                {
                    System.Reflection.ParameterInfo parameter = parameters[i];
                    if (modelDataSourceMethod.Parameters.Contains(parameter.Name)) { continue; }
                    object value;
                    if (BindFromAttribute(ec, modelState, parameter, out value))
                    {
                        modelDataSourceMethod.Parameters[parameter.Name] = value;
                    }
                }
            }

            // Resolves a parameter through its ValueProviderSourceAttribute (here [QueryString])
            // using the real value provider + DefaultModelBinder.
            private static bool BindFromAttribute(
                ModelBindingExecutionContext ec, ModelStateDictionary modelState,
                System.Reflection.ParameterInfo parameter, out object value)
            {
                value = null;
                object[] attributes = parameter.GetCustomAttributes(typeof(ValueProviderSourceAttribute), false);
                if (attributes == null || attributes.Length == 0) { return false; }
                ValueProviderSourceAttribute source = (ValueProviderSourceAttribute)attributes[0];
                IValueProvider provider = source.GetValueProvider(ec);
                string modelName = parameter.Name;
                string overrideName = source.GetModelName();
                if (!string.IsNullOrEmpty(overrideName)) { modelName = overrideName; }
                if (provider == null || !provider.ContainsPrefix(modelName)) { return false; }

                ModelBindingContext ctx = new ModelBindingContext();
                ctx.ModelMetadata = new ModelMetadata(null, null, null, parameter.ParameterType, parameter.Name);
                ctx.ModelName = modelName;
                ctx.ModelState = modelState;
                ctx.ValueProvider = provider;
                DefaultModelBinder binder = new DefaultModelBinder();
                if (binder.BindModel(ec, ctx))
                {
                    value = ctx.Model;
                    return true;
                }
                return false;
            }
        }

        // Drives the ModelDataSource select pipeline: the [QueryString] "category" parameter is
        // resolved from the request query string by the real value provider + DefaultModelBinder,
        // the select method runs, and the returned rows flow back through CreateSelectResult.
        // Returns:
        //   [0] resolved category (proven by match) (string) "tools"
        //   [1] number of rows returned             (int)    3
        //   [2] first product name                  (string) "Widget"
        public static object[] ModelDataSourceSelectWithQueryString()
        {
            NameValueCollection query = new NameValueCollection { { "category", "tools" } };
            HttpContextBase httpContext = new FakeBindingContext(new FakeBindingRequest(null, query));

            Control dataControl = new ProductsHostControl();
            TestModelDataSource dataSource = new TestModelDataSource(dataControl, httpContext);
            ProductRepository repository = new ProductRepository();
            // Supply the data-methods object so FindMethod does not depend on a Page.
            dataSource.CallingDataMethods += delegate (object sender, CallingDataMethodsEventArgs e)
            {
                e.DataMethodsObject = repository;
                e.DataMethodsType = typeof(ProductRepository);
            };
            dataSource.UpdateProperties(typeof(Product).FullName, "GetProducts");

            ModelDataSourceView view = dataSource.View;
            DataSourceSelectArguments args = DataSourceSelectArguments.Empty;
            // ExecuteSelect is protected internal; drive it through the public callback API.
            IEnumerable result = null;
            view.Select(args, delegate (IEnumerable data) { result = data; });

            int count = 0;
            string firstName = null;
            if (result != null)
            {
                foreach (object row in result)
                {
                    Product p = row as Product;
                    if (count == 0 && p != null) { firstName = p.Name; }
                    count++;
                }
            }

            return new object[]
            {
                "tools",
                count,
                firstName,
            };
        }
    }
}
