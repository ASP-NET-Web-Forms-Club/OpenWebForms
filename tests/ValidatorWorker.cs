using System;
using System.Reflection;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace System.Web.Tests
{
    // Worker executed INSIDE the custom AssemblyLoadContext (via RunInAlc), so the validator
    // types bind to OUR clean-room System.Web. Builds a page with input controls + validators,
    // runs Init so validators register with Page.Validators and the tree is searchable by id,
    // then exercises each validator's EvaluateIsValid through the public Validate() surface and
    // Page.Validate() aggregation.
    internal static class ValidatorWorker
    {
        private static readonly BindingFlags Inst =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static void Init(Control c)
        {
            MethodInfo mi = typeof(Control).GetMethod("InitRecursive", Inst);
            mi.Invoke(c, new object[] { null });
        }

        // Returns object[] of bools:
        //   [0] requiredEmptyInvalid   -- RequiredFieldValidator with empty input -> IsValid false
        //   [1] requiredFilledValid    -- ... with a value -> IsValid true
        //   [2] rangeInRangeValid      -- RangeValidator value within [1,10] -> true
        //   [3] rangeOutOfRangeInvalid -- ... value above 10 -> false
        //   [4] compareEqualValid      -- CompareValidator equal value -> true
        //   [5] compareNotEqualInvalid -- ... unequal value -> false
        //   [6] regexMatchValid        -- RegularExpressionValidator matching pattern -> true
        //   [7] regexNoMatchInvalid    -- ... non-matching -> false
        //   [8] pageValidAllPass       -- Page.Validate() -> Page.IsValid true when all pass
        //   [9] pageInvalidOneFails    -- Page.Validate() -> Page.IsValid false when one fails
        public static object[] Run()
        {
            // ---------- RequiredFieldValidator ----------
            bool requiredEmptyInvalid;
            bool requiredFilledValid;
            {
                ValidatorPage page = new ValidatorPage();
                TextBox box = new TextBox(); box.ID = "box"; page.Controls.Add(box);
                RequiredFieldValidator rfv = new RequiredFieldValidator();
                rfv.ID = "rfv"; rfv.ControlToValidate = "box"; rfv.ErrorMessage = "required";
                page.Controls.Add(rfv);
                Init(page);

                box.Text = "";
                rfv.Validate();
                requiredEmptyInvalid = !rfv.IsValid;

                box.Text = "something";
                rfv.Validate();
                requiredFilledValid = rfv.IsValid;
            }

            // ---------- RangeValidator ----------
            bool rangeInRangeValid;
            bool rangeOutOfRangeInvalid;
            {
                ValidatorPage page = new ValidatorPage();
                TextBox box = new TextBox(); box.ID = "num"; page.Controls.Add(box);
                RangeValidator rv = new RangeValidator();
                rv.ID = "rv"; rv.ControlToValidate = "num";
                rv.MinimumValue = "1"; rv.MaximumValue = "10";
                rv.Type = ValidationDataType.Integer;
                page.Controls.Add(rv);
                Init(page);

                box.Text = "5";
                rv.Validate();
                rangeInRangeValid = rv.IsValid;

                box.Text = "42";
                rv.Validate();
                rangeOutOfRangeInvalid = !rv.IsValid;
            }

            // ---------- CompareValidator ----------
            bool compareEqualValid;
            bool compareNotEqualInvalid;
            {
                ValidatorPage page = new ValidatorPage();
                TextBox box = new TextBox(); box.ID = "cmp"; page.Controls.Add(box);
                CompareValidator cv = new CompareValidator();
                cv.ID = "cv"; cv.ControlToValidate = "cmp";
                cv.ValueToCompare = "100"; cv.Operator = ValidationCompareOperator.Equal;
                cv.Type = ValidationDataType.Integer;
                page.Controls.Add(cv);
                Init(page);

                box.Text = "100";
                cv.Validate();
                compareEqualValid = cv.IsValid;

                box.Text = "99";
                cv.Validate();
                compareNotEqualInvalid = !cv.IsValid;
            }

            // ---------- RegularExpressionValidator ----------
            bool regexMatchValid;
            bool regexNoMatchInvalid;
            {
                ValidatorPage page = new ValidatorPage();
                TextBox box = new TextBox(); box.ID = "email"; page.Controls.Add(box);
                RegularExpressionValidator rev = new RegularExpressionValidator();
                rev.ID = "rev"; rev.ControlToValidate = "email";
                rev.ValidationExpression = "[a-z]+@[a-z]+\\.[a-z]+";
                page.Controls.Add(rev);
                Init(page);

                box.Text = "user@example.com";
                rev.Validate();
                regexMatchValid = rev.IsValid;

                box.Text = "not-an-email";
                rev.Validate();
                regexNoMatchInvalid = !rev.IsValid;
            }

            // ---------- Page.Validate() aggregation ----------
            bool pageValidAllPass;
            bool pageInvalidOneFails;
            {
                ValidatorPage page = new ValidatorPage();
                TextBox box = new TextBox(); box.ID = "field"; page.Controls.Add(box);
                RequiredFieldValidator rfv = new RequiredFieldValidator();
                rfv.ID = "agg_rfv"; rfv.ControlToValidate = "field"; rfv.ErrorMessage = "required";
                page.Controls.Add(rfv);
                RangeValidator rv = new RangeValidator();
                rv.ID = "agg_rv"; rv.ControlToValidate = "field";
                rv.MinimumValue = "1"; rv.MaximumValue = "10"; rv.Type = ValidationDataType.Integer;
                page.Controls.Add(rv);
                Init(page);

                // All pass: a value that is both non-empty and in range.
                box.Text = "5";
                page.Validate();
                pageValidAllPass = page.IsValid;

                // One fails: out of range (RangeValidator) though non-empty.
                box.Text = "99";
                page.Validate();
                pageInvalidOneFails = !page.IsValid;
            }

            return new object[]
            {
                requiredEmptyInvalid,
                requiredFilledValid,
                rangeInRangeValid,
                rangeOutOfRangeInvalid,
                compareEqualValid,
                compareNotEqualInvalid,
                regexMatchValid,
                regexNoMatchInvalid,
                pageValidAllPass,
                pageInvalidOneFails,
            };
        }
    }

    // Plain page used as a naming container/host for validators and their target controls.
    internal sealed class ValidatorPage : Page
    {
    }
}
