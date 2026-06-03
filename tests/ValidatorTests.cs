using System;
using Xunit;

namespace System.Web.Tests
{
    // Tier-5a validator gate. Drives ValidatorWorker INSIDE the ALC (so the validator types and
    // Page bind to OUR clean-room System.Web) and asserts on the returned facts:
    //   * RequiredFieldValidator: empty -> invalid, value -> valid;
    //   * RangeValidator: in-range -> valid, out-of-range -> invalid;
    //   * CompareValidator: equal -> valid, not-equal -> invalid;
    //   * RegularExpressionValidator: match -> valid, no-match -> invalid;
    //   * Page.Validate() aggregates the per-validator results into Page.IsValid.
    // Deterministic and cross-platform: integer/regex comparisons, no clock, no culture output.
    public class ValidatorTests
    {
        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        [Fact]
        public void ValidatorsEvaluateAndPageAggregates()
        {
            object[] r = (object[])Web.RunInAlc("System.Web.Tests.ValidatorWorker", "Run");

            bool requiredEmptyInvalid = (bool)r[0];
            bool requiredFilledValid = (bool)r[1];
            bool rangeInRangeValid = (bool)r[2];
            bool rangeOutOfRangeInvalid = (bool)r[3];
            bool compareEqualValid = (bool)r[4];
            bool compareNotEqualInvalid = (bool)r[5];
            bool regexMatchValid = (bool)r[6];
            bool regexNoMatchInvalid = (bool)r[7];
            bool pageValidAllPass = (bool)r[8];
            bool pageInvalidOneFails = (bool)r[9];

            // RequiredFieldValidator
            Assert.True(requiredEmptyInvalid, "RequiredFieldValidator should be invalid for empty input");
            Assert.True(requiredFilledValid, "RequiredFieldValidator should be valid for non-empty input");

            // RangeValidator
            Assert.True(rangeInRangeValid, "RangeValidator should be valid for an in-range value");
            Assert.True(rangeOutOfRangeInvalid, "RangeValidator should be invalid for an out-of-range value");

            // CompareValidator
            Assert.True(compareEqualValid, "CompareValidator should be valid when values are equal");
            Assert.True(compareNotEqualInvalid, "CompareValidator should be invalid when values differ");

            // RegularExpressionValidator
            Assert.True(regexMatchValid, "RegularExpressionValidator should be valid for a matching value");
            Assert.True(regexNoMatchInvalid, "RegularExpressionValidator should be invalid for a non-matching value");

            // Page.Validate() aggregation -> Page.IsValid
            Assert.True(pageValidAllPass, "Page.IsValid should be true when all validators pass");
            Assert.True(pageInvalidOneFails, "Page.IsValid should be false when any validator fails");
        }
    }
}
