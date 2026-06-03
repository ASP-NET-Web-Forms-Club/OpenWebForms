// Clean-room runtime CodeGen + Roslyn compilation for parsed .aspx/.ascx pages.
//
// This file fills the BuildManager / PageHandlerFactory / SimpleHandlerFactory runtime entry
// points (declared as throw-only skeletons in System.Web.Compilation.cs / System.Web.UI.cs) plus
// the internal codegen + compile machinery. It is implemented strictly from public/documented
// behavior:
//   * The .aspx/.ascx markup grammar (a documented file format) is parsed by the existing
//     clean-room TemplateParser into a ControlBuilder tree + directive metadata.
//   * From that tree we EMIT C# source for a partial class deriving from the @Page/@Control
//     `Inherits` type (or System.Web.UI.Page / UserControl by default). The generated
//     FrameworkInitialize override instantiates each control, sets its ID + tag attributes
//     (settable CLR property, else IAttributeAccessor.SetAttribute), nests children via
//     IParserAccessor.AddParsedSubObject (controls + LiteralControl for literal text), assigns
//     ITemplate properties via generated nested template classes, and wires events from
//     OnXxx="Handler" attributes + AutoEventWireup (Page_Load / Page_Init by name).
//   * Roslyn (Microsoft.CodeAnalysis.CSharp) compiles the generated source (+ optional code-behind
//     file) against OUR System.Web.dll + the trusted-platform BCL + app bin, emits to a
//     MemoryStream, and loads the assembly INTO THE SAME AssemblyLoadContext as System.Web so the
//     generated page binds to OURS.
//
// NO LINQ (explicit for/foreach). All NEW types are internal.
#nullable disable
#pragma warning disable

namespace System.Web.Compilation
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;
    using System.Text;
    using SWUI = System.Web.UI;

    // ------------------------------------------------------------------------------------------
    // Builder-tree -> C# source generator.
    // ------------------------------------------------------------------------------------------
    internal sealed class TemplateCodeGenerator
    {
        private readonly SWUI.TemplateParser _parser;
        private readonly string _virtualPath;
        private readonly bool _isUserControl;
        // True when generating the application file (global.asax): the result is a plain
        // HttpApplication-derived class carrying the declaration-block members (the magic event
        // methods), with NO control tree / FrameworkInitialize / AutoEventWireup.
        private readonly bool _isApplication;

        private readonly StringBuilder _body = new StringBuilder();          // __BuildControlTree + helpers
        private readonly List<string> _nestedClasses = new List<string>();   // nested ITemplate classes
        private readonly List<string> _fieldDecls = new List<string>();      // protected control fields
        private readonly Dictionary<string, bool> _fieldNames = new Dictionary<string, bool>(StringComparer.Ordinal);
        private int _ctrlCounter;
        private int _templateCounter;
        // >0 while emitting inside a nested ITemplate class, where `this` is the template (not the
        // page), so control fields must NOT be hoisted onto `this`.
        private int _inTemplate;
        private string _className;
        private string _baseTypeName;
        private Type _baseType;
        private bool _hasCodeBehind;

        internal TemplateCodeGenerator(SWUI.TemplateParser parser, string virtualPath, bool isUserControl)
            : this(parser, virtualPath, isUserControl, false)
        {
        }

        internal TemplateCodeGenerator(SWUI.TemplateParser parser, string virtualPath, bool isUserControl, bool isApplication)
        {
            _parser = parser;
            _virtualPath = virtualPath;
            _isUserControl = isUserControl;
            _isApplication = isApplication;
        }

        internal string GeneratedClassName { get { return _className; } }
        internal Type ResolvedBaseType { get { return _baseType; } }

        private void ResolveBaseType()
        {
            string inherits = _parser.InheritsBaseTypeName;
            Type t = null;
            if (inherits != null && inherits.Length != 0)
            {
                t = ResolveType(inherits);
            }
            if (t == null) { t = _parser.DefaultBaseType; }
            if (t == null) { t = _isUserControl ? typeof(SWUI.UserControl) : typeof(SWUI.Page); }
            _baseType = t;
            _baseTypeName = TypeRef(t);
        }

        private static Type ResolveType(string typeName)
        {
            Type t = Type.GetType(typeName, false, true);
            if (t != null) { return t; }
            Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < asms.Length; i++)
            {
                try
                {
                    Type c = asms[i].GetType(typeName, false, true);
                    if (c != null) { return c; }
                }
                catch (Exception) { }
            }
            return null;
        }

        private void ResolveClassName()
        {
            string explicitName = _parser.GeneratedClassName;
            if (explicitName != null && explicitName.Length != 0)
            {
                _className = SanitizeIdentifier(explicitName);
                return;
            }
            string name = _virtualPath;
            if (name == null) { name = "page"; }
            int slash = name.LastIndexOfAny(new char[] { '/', '\\' });
            if (slash >= 0) { name = name.Substring(slash + 1); }
            name = name.Replace('.', '_');
            _className = SanitizeIdentifier(name);
            if (_className.Length == 0) { _className = "page_aspx"; }
        }

        internal string Generate()
        {
            ResolveBaseType();
            ResolveClassName();
            string cf = _parser.CodeFileName;
            _hasCodeBehind = (cf != null && cf.Length != 0);

            // global.asax: emit a plain HttpApplication-derived class carrying the declaration
            // blocks (the magic event methods). No control tree / FrameworkInitialize.
            if (_isApplication)
            {
                return GenerateApplication();
            }

            // ----- build the tree body into _body -----
            _body.Append("private void __BuildControlTree(").Append(_baseTypeName).Append(" __ctrl) {\r\n");
            SWUI.RootBuilder root = _parser.RootBuilderInternal;
            string masterFile = _isUserControl ? null : _parser.MasterPageFileInternal;
            if (masterFile != null && masterFile.Length != 0)
            {
                // Content page: declare the master and register each <asp:Content> region's markup as
                // a content template keyed by its ContentPlaceHolderID. The page's ApplyMasterPage
                // (after PreInit) hands these to the master and swaps in the master's tree.
                _body.Append("__ctrl.MasterPageFile = ").Append(Quote(masterFile)).Append(";\r\n");
                EmitContentRegions(_body, root);
            }
            else
            {
                EmitContainerChildren(_body, "__ctrl", root);
            }
            if (!_isUserControl) { EmitAutoEventWireup(_body); }
            _body.Append("}\r\n");


            // ----- assemble the full compilation unit -----
            StringBuilder sb = new StringBuilder();
            sb.Append("// <auto-generated> generated from ").Append(_virtualPath == null ? "(inline)" : _virtualPath).Append("\r\n");
            sb.Append("#pragma warning disable\r\n");
            sb.Append("namespace ASP {\r\n");
            sb.Append("using System;\r\n");
            sb.Append("using System.Web;\r\n");
            sb.Append("using System.Web.UI;\r\n");
            sb.Append("using System.Web.UI.WebControls;\r\n");
            sb.Append("using System.Web.UI.HtmlControls;\r\n");
            List<string> imp = _parser.NamespacesInternal;
            if (imp != null)
            {
                for (int i = 0; i < imp.Count; i++) { sb.Append("using ").Append(imp[i]).Append(";\r\n"); }
            }

            sb.Append("public partial class ").Append(_className).Append(" : ").Append(_baseTypeName).Append(" {\r\n");

            // Protected control fields (when no code-behind declares them).
            for (int i = 0; i < _fieldDecls.Count; i++) { sb.Append(_fieldDecls[i]); }

            // Nested ITemplate classes.
            for (int i = 0; i < _nestedClasses.Count; i++) { sb.Append(_nestedClasses[i]); }

            // Inline <script runat=server> code declarations become class members.
            List<string> codeBlocks = _parser.CodeBlocksInternal;
            if (codeBlocks != null)
            {
                for (int i = 0; i < codeBlocks.Count; i++)
                {
                    string code = codeBlocks[i];
                    if (code != null && code.Length != 0) { sb.Append(code).Append("\r\n"); }
                }
            }

            // FrameworkInitialize override builds the tree.
            sb.Append("protected override void FrameworkInitialize() {\r\n");
            sb.Append("base.FrameworkInitialize();\r\n");
            sb.Append("this.__BuildControlTree(this);\r\n");
            sb.Append("}\r\n");

            sb.Append(_body.ToString());

            sb.Append("}\r\n");   // class
            sb.Append("}\r\n");   // namespace
            return sb.ToString();
        }

        // Emit the global.asax compilation unit: a partial class deriving from the resolved base
        // (HttpApplication, or the @Application Inherits type / DefaultApplicationBaseType) whose
        // only members are the inline <script runat="server"> declaration blocks. When a
        // code-behind (CodeFile/CodeBehind) supplies the real application class, the Inherits type
        // already carries the magic methods and this generated subclass simply inherits them; the
        // runtime binds them by name on the instance type.
        private string GenerateApplication()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("// <auto-generated> generated from ").Append(_virtualPath == null ? "(inline)" : _virtualPath).Append("\r\n");
            sb.Append("#pragma warning disable\r\n");
            sb.Append("namespace ASP {\r\n");
            sb.Append("using System;\r\n");
            sb.Append("using System.Web;\r\n");
            sb.Append("using System.Web.UI;\r\n");
            List<string> imp = _parser.NamespacesInternal;
            if (imp != null)
            {
                for (int i = 0; i < imp.Count; i++) { sb.Append("using ").Append(imp[i]).Append(";\r\n"); }
            }

            sb.Append("public partial class ").Append(_className).Append(" : ").Append(_baseTypeName).Append(" {\r\n");

            // Inline <script runat=server> code declarations become class members (the magic
            // Application_*/Session_* event methods plus any helpers/fields the app declares).
            List<string> codeBlocks = _parser.CodeBlocksInternal;
            if (codeBlocks != null)
            {
                for (int i = 0; i < codeBlocks.Count; i++)
                {
                    string code = codeBlocks[i];
                    if (code != null && code.Length != 0) { sb.Append(code).Append("\r\n"); }
                }
            }

            sb.Append("}\r\n");   // class
            sb.Append("}\r\n");   // namespace
            return sb.ToString();
        }

        private void EmitAutoEventWireup(StringBuilder w)
        {
            if (!_parser.AutoEventWireupInternal) { return; }
            EmitAutoHook(w, "Page_PreInit", "this.PreInit");
            EmitAutoHook(w, "Page_Init", "this.Init");
            EmitAutoHook(w, "Page_InitComplete", "this.InitComplete");
            EmitAutoHook(w, "Page_PreLoad", "this.PreLoad");
            EmitAutoHook(w, "Page_Load", "this.Load");
            EmitAutoHook(w, "Page_LoadComplete", "this.LoadComplete");
            EmitAutoHook(w, "Page_PreRender", "this.PreRender");
            EmitAutoHook(w, "Page_PreRenderComplete", "this.PreRenderComplete");
            EmitAutoHook(w, "Page_DataBind", "this.DataBinding");
            EmitAutoHook(w, "Page_Unload", "this.Unload");
            EmitAutoHook(w, "Page_Error", "this.Error");
        }

        // Emit the handler subscription only when a conventionally-named method (arity 2) is
        // discoverable on the resolved base type at codegen time; otherwise skip it (a page with
        // AutoEventWireup but no Page_Load is valid).
        private void EmitAutoHook(StringBuilder w, string methodName, string eventExpr)
        {
            if (!HasAutoHandler(methodName)) { return; }
            w.Append(eventExpr).Append(" += new System.EventHandler(this.").Append(methodName).Append(");\r\n");
        }

        // True when a conventionally-named handler is discoverable: either declared on the resolved
        // base type (typical for a compiled code-behind base), present in an inline
        // <script runat=server> declaration block, or (conservatively) when a code-behind file is
        // attached. The handler is referenced as `this.<methodName>` in the generated tree, so it
        // must resolve on the generated partial class.
        private bool HasAutoHandler(string methodName)
        {
            if (_baseType != null)
            {
                try
                {
                    MethodInfo[] ms = _baseType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                    for (int i = 0; i < ms.Length; i++)
                    {
                        if (string.Equals(ms[i].Name, methodName, StringComparison.Ordinal) && ms[i].GetParameters().Length == 2)
                        {
                            return true;
                        }
                    }
                }
                catch (Exception) { }
            }
            // Inline <script runat=server> declaration bodies.
            List<string> codeBlocks = _parser.CodeBlocksInternal;
            if (codeBlocks != null)
            {
                for (int i = 0; i < codeBlocks.Count; i++)
                {
                    string cb = codeBlocks[i];
                    if (cb != null && DeclaresMethod(cb, methodName)) { return true; }
                }
            }
            return false;
        }

        // Heuristic: the body declares `methodName` as a method if the identifier appears followed
        // (after optional whitespace) by an opening parenthesis, and is not part of a longer
        // identifier. Good enough to detect Page_Load / Page_Init in a declaration block.
        private static bool DeclaresMethod(string src, string methodName)
        {
            int idx = 0;
            while (true)
            {
                idx = src.IndexOf(methodName, idx, StringComparison.Ordinal);
                if (idx < 0) { return false; }
                bool leftOk = (idx == 0) || !IsIdentChar(src[idx - 1]);
                int after = idx + methodName.Length;
                int j = after;
                while (j < src.Length && char.IsWhiteSpace(src[j])) { j++; }
                bool rightOk = (j < src.Length && src[j] == '(');
                if (leftOk && rightOk) { return true; }
                idx = after;
            }
        }

        private static bool IsIdentChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        private void EmitContainerChildren(StringBuilder w, string containerVar, SWUI.ControlBuilder builder)
        {
            if (builder == null) { return; }
            ArrayList subs = builder.SubBuilders;
            if (subs == null) { return; }
            for (int i = 0; i < subs.Count; i++)
            {
                object item = subs[i];
                string literal = item as string;
                if (literal != null)
                {
                    if (literal.Length == 0) { continue; }
                    w.Append("((System.Web.UI.IParserAccessor)").Append(containerVar)
                     .Append(").AddParsedSubObject(new System.Web.UI.LiteralControl(")
                     .Append(Quote(literal)).Append("));\r\n");
                    continue;
                }
                SWUI.CodeBlockBuilder cb = item as SWUI.CodeBlockBuilder;
                if (cb != null)
                {
                    EmitCodeBlock(w, containerVar, cb);
                    continue;
                }
                SWUI.ControlBuilder child = item as SWUI.ControlBuilder;
                if (child != null && child.ControlType != null)
                {
                    string childVar = EmitBuildControl(w, child);
                    w.Append("((System.Web.UI.IParserAccessor)").Append(containerVar)
                     .Append(").AddParsedSubObject(").Append(childVar).Append(");\r\n");
                }
            }
        }

        // Code render blocks. <%= expr %> / <%# expr %> render the expression value; <% code %> is
        // statement code. For the common page case we add a LiteralControl carrying the expression
        // value (eager Convert.ToString) so the rendered output reflects page state.
        private void EmitCodeBlock(StringBuilder w, string containerVar, SWUI.CodeBlockBuilder cb)
        {
            string code = cb.Code;
            if (code == null) { return; }
            if (cb.BlockType == SWUI.CodeBlockType.Expression || cb.BlockType == SWUI.CodeBlockType.DataBinding)
            {
                w.Append("((System.Web.UI.IParserAccessor)").Append(containerVar)
                 .Append(").AddParsedSubObject(new System.Web.UI.LiteralControl(System.Convert.ToString(")
                 .Append(code).Append(")));\r\n");
            }
            else
            {
                // <% statements %> : emit inline (executes during tree build).
                w.Append(code).Append("\r\n");
            }
        }

        // Emit code that constructs the control for `builder` into a fresh local; returns its name.
        private string EmitBuildControl(StringBuilder w, SWUI.ControlBuilder builder)
        {
            Type ct = builder.ControlType;
            string typeRef = TypeRef(ct);
            string var = "__c" + (_ctrlCounter++).ToString(CultureInfo.InvariantCulture);

            w.Append(typeRef).Append(" ").Append(var).Append(" = new ").Append(typeRef).Append("();\r\n");

            string id = builder.ID;
            if (id != null && id.Length != 0)
            {
                w.Append(var).Append(".ID = ").Append(Quote(id)).Append(";\r\n");
                // Expose the control as a protected member so inline <script runat=server> code (and
                // strongly-typed access) can reference it by ID, mirroring the ASP.NET generated
                // partial class. Skipped when a code-behind file is present (it declares the fields).
                if (!_hasCodeBehind && _inTemplate == 0)
                {
                    string fieldName = SanitizeIdentifier(id);
                    if (fieldName.Length != 0 && !_fieldNames.ContainsKey(fieldName))
                    {
                        _fieldNames[fieldName] = true;
                        _fieldDecls.Add("protected " + typeRef + " " + fieldName + ";\r\n");
                        w.Append("this.").Append(fieldName).Append(" = ").Append(var).Append(";\r\n");
                    }
                }
            }

            IDictionary attribs = builder.AttributesInternal;
            if (attribs != null) { EmitAttributes(w, var, ct, attribs); }

            EmitContainerChildren(w, var, builder);

            List<SWUI.ComplexPropertyBuilderEntry> complex = builder.ComplexPropertyBuilders;
            if (complex != null)
            {
                for (int i = 0; i < complex.Count; i++) { EmitComplexProperty(w, var, ct, complex[i]); }
            }

            return var;
        }

        private void EmitAttributes(StringBuilder w, string var, Type ct, IDictionary attribs)
        {
            IDictionaryEnumerator e = attribs.GetEnumerator();
            while (e.MoveNext())
            {
                string name = e.Key as string;
                if (name == null) { continue; }
                if (string.Equals(name, "id", StringComparison.OrdinalIgnoreCase)) { continue; }
                if (string.Equals(name, "runat", StringComparison.OrdinalIgnoreCase)) { continue; }
                string value = e.Value as string;

                EventInfo ev = FindEvent(ct, name);
                if (ev != null && value != null && value.Length != 0)
                {
                    string delType = TypeRef(ev.EventHandlerType);
                    w.Append(var).Append(".").Append(ev.Name)
                     .Append(" += new ").Append(delType).Append("(this.").Append(value).Append(");\r\n");
                    continue;
                }

                PropertyInfo pi = FindSettableProperty(ct, name);
                if (pi != null)
                {
                    string assign = EmitPropertyValue(pi.PropertyType, value);
                    if (assign != null)
                    {
                        w.Append(var).Append(".").Append(pi.Name).Append(" = ").Append(assign).Append(";\r\n");
                        continue;
                    }
                }

                w.Append("((System.Web.UI.IAttributeAccessor)").Append(var)
                 .Append(").SetAttribute(").Append(Quote(name)).Append(", ").Append(Quote(value)).Append(");\r\n");
            }
        }

        // Emit a C# expression assigning `value` to a property of the given type, or null if the
        // conversion cannot be represented inline (caller then falls back to SetAttribute).
        private string EmitPropertyValue(Type propType, string value)
        {
            Type underlying = Nullable.GetUnderlyingType(propType);
            Type t = underlying != null ? underlying : propType;
            if (t == typeof(string)) { return Quote(value); }
            if (value == null) { return null; }
            if (t == typeof(bool))
            {
                if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)) { return "true"; }
                if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)) { return "false"; }
                return null;
            }
            if (t.IsEnum)
            {
                try
                {
                    object parsed = Enum.Parse(t, value, true);
                    return TypeRef(t) + "." + parsed.ToString();
                }
                catch (Exception) { return null; }
            }
            if (t == typeof(int) || t == typeof(short) || t == typeof(long) || t == typeof(byte))
            {
                long n;
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                {
                    return "(" + TypeRef(t) + ")(" + n.ToString(CultureInfo.InvariantCulture) + ")";
                }
                return null;
            }
            if (t == typeof(double) || t == typeof(float) || t == typeof(decimal))
            {
                double d;
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                {
                    string suffix = (t == typeof(float)) ? "f" : (t == typeof(decimal)) ? "m" : "";
                    return d.ToString("R", CultureInfo.InvariantCulture) + suffix;
                }
                return null;
            }
            // Richer types (Unit, Color, ...): parse via the type's converter at build time.
            return "(" + TypeRef(propType) + ")(System.ComponentModel.TypeDescriptor.GetConverter(typeof(" +
                   TypeRef(t) + ")).ConvertFromInvariantString(" + Quote(value) + "))";
        }

        private void EmitComplexProperty(StringBuilder w, string var, Type ct, SWUI.ComplexPropertyBuilderEntry entry)
        {
            PropertyInfo pi = entry.Property;
            if (pi == null) { return; }
            SWUI.ControlBuilder b = entry.Builder;

            SWUI.TemplateBuilder tb = b as SWUI.TemplateBuilder;
            if (tb != null && typeof(SWUI.ITemplate).IsAssignableFrom(pi.PropertyType) && pi.CanWrite)
            {
                string tmplClass = EmitTemplateClass(tb);
                w.Append(var).Append(".").Append(pi.Name).Append(" = new ").Append(tmplClass).Append("();\r\n");
                return;
            }

            if (typeof(IList).IsAssignableFrom(pi.PropertyType) && b != null && pi.GetGetMethod() != null)
            {
                ArrayList subs = b.SubBuilders;
                if (subs != null)
                {
                    for (int j = 0; j < subs.Count; j++)
                    {
                        SWUI.ControlBuilder cbld = subs[j] as SWUI.ControlBuilder;
                        if (cbld == null || cbld.ControlType == null) { continue; }
                        string childVar = EmitBuildControl(w, cbld);
                        w.Append(var).Append(".").Append(pi.Name).Append(".Add(").Append(childVar).Append(");\r\n");
                    }
                }
                return;
            }

            if (b != null && b.ControlType != null && pi.CanWrite)
            {
                string childVar = EmitBuildControl(w, b);
                w.Append(var).Append(".").Append(pi.Name).Append(" = ").Append(childVar).Append(";\r\n");
            }
        }

        // Emit a nested ITemplate class that replays the template tree into the target container.
        private string EmitTemplateClass(SWUI.TemplateBuilder tb)
        {
            return EmitTemplateClassFor(tb);
        }

        // Emit a nested ITemplate class whose InstantiateIn replays the children of `builder`.
        // Works for any ControlBuilder (TemplateBuilder or, for master merge, a <asp:Content> builder).
        private string EmitTemplateClassFor(SWUI.ControlBuilder builder)
        {
            string clsName = "__Template_" + (_templateCounter++).ToString(CultureInfo.InvariantCulture);
            StringBuilder w = new StringBuilder();
            w.Append("private sealed class ").Append(clsName).Append(" : System.Web.UI.ITemplate {\r\n");
            w.Append("public void InstantiateIn(System.Web.UI.Control container) {\r\n");
            _inTemplate++;
            EmitContainerChildren(w, "container", builder);
            _inTemplate--;
            w.Append("}\r\n}\r\n");
            _nestedClasses.Add(w.ToString());
            return clsName;
        }

        // Content-page root: register each top-level <asp:Content> region's markup as a content
        // template keyed by its ContentPlaceHolderID. Any stray literal/non-Content content at the
        // root of a content page is ignored (ASP.NET disallows it; we simply drop it).
        private void EmitContentRegions(StringBuilder w, SWUI.ControlBuilder root)
        {
            if (root == null) { return; }
            ArrayList subs = root.SubBuilders;
            if (subs == null) { return; }
            for (int i = 0; i < subs.Count; i++)
            {
                SWUI.ControlBuilder child = subs[i] as SWUI.ControlBuilder;
                if (child == null || child.ControlType == null) { continue; }
                if (!typeof(SWUI.WebControls.Content).IsAssignableFrom(child.ControlType)) { continue; }
                string phId = GetContentPlaceHolderID(child);
                if (phId == null) { phId = ""; }
                string tmplClass = EmitTemplateClassFor(child);
                // AddContentTemplate is protected on Page; call it through `this` (the generated page
                // derives from Page) rather than the base-typed __ctrl parameter.
                w.Append("this.AddContentTemplate(").Append(Quote(phId)).Append(", new ").Append(tmplClass).Append("());\r\n");
            }
        }

        private static string GetContentPlaceHolderID(SWUI.ControlBuilder builder)
        {
            IDictionary attribs = builder.AttributesInternal;
            if (attribs == null) { return null; }
            IDictionaryEnumerator e = attribs.GetEnumerator();
            while (e.MoveNext())
            {
                string key = e.Key as string;
                if (key != null && string.Equals(key, "contentplaceholderid", StringComparison.OrdinalIgnoreCase))
                {
                    return e.Value as string;
                }
            }
            return null;
        }

        // ---- helpers ----

        private static PropertyInfo FindSettableProperty(Type t, string name)
        {
            try
            {
                PropertyInfo pi = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (pi != null && pi.CanWrite && pi.GetSetMethod() != null) { return pi; }
            }
            catch (Exception) { }
            return null;
        }

        private static EventInfo FindEvent(Type t, string attrName)
        {
            string evName = attrName;
            if (attrName.Length > 2 && (attrName[0] == 'O' || attrName[0] == 'o') && (attrName[1] == 'n' || attrName[1] == 'N'))
            {
                evName = attrName.Substring(2);
            }
            try
            {
                EventInfo[] evs = t.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                for (int i = 0; i < evs.Length; i++)
                {
                    if (string.Equals(evs[i].Name, evName, StringComparison.OrdinalIgnoreCase)) { return evs[i]; }
                }
            }
            catch (Exception) { }
            return null;
        }

        internal static string TypeRef(Type t)
        {
            if (t == null) { return "object"; }
            if (t.IsGenericType)
            {
                Type def = t.GetGenericTypeDefinition();
                string baseName = def.FullName;
                int tick = baseName.IndexOf('`');
                if (tick >= 0) { baseName = baseName.Substring(0, tick); }
                StringBuilder sb = new StringBuilder();
                sb.Append("global::").Append(baseName.Replace('+', '.')).Append("<");
                Type[] args = t.GetGenericArguments();
                for (int i = 0; i < args.Length; i++)
                {
                    if (i > 0) { sb.Append(", "); }
                    sb.Append(TypeRef(args[i]));
                }
                sb.Append(">");
                return sb.ToString();
            }
            return "global::" + t.FullName.Replace('+', '.');
        }

        private static string Quote(string s)
        {
            if (s == null) { return "null"; }
            StringBuilder sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) { sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture)); }
                        else { sb.Append(c); }
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        private static string SanitizeIdentifier(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsLetterOrDigit(c) || c == '_') { sb.Append(c); }
                else { sb.Append('_'); }
            }
            if (sb.Length > 0 && char.IsDigit(sb[0])) { sb.Insert(0, '_'); }
            return sb.ToString();
        }
    }
}