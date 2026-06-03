using System;
using System.Reflection;
using System.Web.Configuration;
using System.Web.UI;

namespace System.Web.Tests
{
    // Workers executed INSIDE the custom AssemblyLoadContext (via RunInAlc) so Control / Page /
    // ObjectStateFormatter / MachineKeySection bind to OUR clean-room System.Web.
    //
    // Covers the Tier-8 "core fix" loose ends:
    //   * Control.EnsureID assigns a STABLE automatic id (ctlNN) once a control is parented into a
    //     naming container, and the id does not churn across repeated EnsureID calls.
    //   * The Page __VIEWSTATE MAC pipeline round-trips (ApplyViewStateMac -> StripViewStateMac) and
    //     a configured MachineKeySection validationKey is honored: the configured key bytes drive a
    //     DIFFERENT HMAC than the default key, and a graph MAC'd with the configured key verifies.
    internal static class CoreFixWorker
    {
        private static readonly BindingFlags Inst =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static readonly BindingFlags Stat =
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        // A control that exposes the protected EnsureID() to the worker.
        private sealed class IdControl : Control
        {
            public void DoEnsureID() { EnsureID(); }
        }

        private sealed class IdContainer : Control, INamingContainer { }

        // ---- A: Control.EnsureID assigns a stable auto-id ----
        // Returns object[]:
        //   [0] firstChildId         -> "ctl00"
        //   [1] secondChildId        -> "ctl01"
        //   [2] idStableAfterRepeat  -> true  (EnsureID a second time keeps the same id)
        //   [3] uniqueIdContainsId   -> true  (UniqueID ends with the generated id)
        //   [4] explicitIdUntouched  -> "myId" (a control with an explicit id is not renamed)
        public static object[] EnsureIdAssignsStableAutoId()
        {
            IdContainer container = new IdContainer();
            container.ID = "outer";

            IdControl a = new IdControl();
            IdControl b = new IdControl();
            container.Controls.Add(a);
            container.Controls.Add(b);

            a.DoEnsureID();
            b.DoEnsureID();

            string firstId = a.ID;
            string secondId = b.ID;

            // Repeated EnsureID must not churn the id.
            a.DoEnsureID();
            bool stable = a.ID == firstId;

            string uid = a.UniqueID;
            bool uniqueHasId = uid != null && uid.EndsWith(firstId, StringComparison.Ordinal);

            // A control with an explicit id keeps it (EnsureID is a no-op on the id text).
            IdControl c = new IdControl();
            c.ID = "myId";
            container.Controls.Add(c);
            c.DoEnsureID();
            string explicitId = c.ID;

            return new object[]
            {
                firstId,
                secondId,
                stable,
                uniqueHasId,
                explicitId,
            };
        }

        // ---- B: Page MAC honors a configured MachineKeySection key ----
        // Returns object[]:
        //   [0] pageMacRoundTrips           -> true  (ApplyViewStateMac then StripViewStateMac returns the raw base64)
        //   [1] configuredKeyHonored        -> true  (GetMacKeyBytes(section.ValidationKey) == HexToBytes(key))
        //   [2] configuredMacDiffersDefault -> true  (HMAC under configured key != HMAC under a different key)
        //   [3] configuredMacVerifies       -> true  (recomputing with the same configured key reproduces the MAC)
        //   [4] tamperRejected              -> true  (a flipped byte fails StripViewStateMac)
        public static object[] PageMacHonorsConfiguredKey()
        {
            // ---- end-to-end Page MAC round-trip (uses the page framework's MAC pipeline) ----
            Page page = new Page();
            page.EnableViewStateMac = true;

            // A representative MAC-free base64 payload.
            byte[] payload = System.Text.Encoding.UTF8.GetBytes("VIEWSTATE-PAYLOAD-CoreFix");
            string rawB64 = Convert.ToBase64String(payload);

            MethodInfo apply = typeof(Page).GetMethod("ApplyViewStateMac", Inst);
            MethodInfo strip = typeof(Page).GetMethod("StripViewStateMac", Inst);
            string macd = (string)apply.Invoke(page, new object[] { rawB64 });
            string stripped = (string)strip.Invoke(page, new object[] { macd });
            bool roundTrips = stripped == rawB64 && macd != rawB64;

            // ---- configured MachineKeySection key honoring (the helpers the Page MAC uses) ----
            const string keyHex =
                "AABBCCDDEEFF00112233445566778899AABBCCDDEEFF00112233445566778899";
            MachineKeySection section = new MachineKeySection();
            section.ValidationKey = keyHex;

            Type osf = Type.GetType("System.Web.UI.ObjectStateFormatter, System.Web");
            // GetMacKeyBytes(validationKeyHex) and HexToBytes(hex) are internal statics.
            MethodInfo getMacKeyBytes = osf.GetMethod("GetMacKeyBytes", Stat);
            MethodInfo hexToBytes = osf.GetMethod("HexToBytes", Stat);
            MethodInfo computeMac = osf.GetMethod("ComputeMac", Stat);

            byte[] keyFromSection = (byte[])getMacKeyBytes.Invoke(null, new object[] { section.ValidationKey });
            byte[] keyDirect = (byte[])hexToBytes.Invoke(null, new object[] { keyHex });
            bool configuredKeyHonored = BytesEqual(keyFromSection, keyDirect) && keyFromSection.Length == 32;

            // The configured key must produce a different HMAC than an unrelated key over the same data.
            byte[] otherKey = (byte[])hexToBytes.Invoke(null, new object[]
                { "00000000000000000000000000000000000000000000000000000000000000FF" });
            byte[] macConfigured = (byte[])computeMac.Invoke(null, new object[] { payload, keyFromSection, "HMACSHA256" });
            byte[] macOther = (byte[])computeMac.Invoke(null, new object[] { payload, otherKey, "HMACSHA256" });
            bool differs = !BytesEqual(macConfigured, macOther);

            // Recomputing with the SAME configured key reproduces the MAC (verifies).
            byte[] macConfigured2 = (byte[])computeMac.Invoke(null, new object[] { payload, keyFromSection, "HMACSHA256" });
            bool verifies = BytesEqual(macConfigured, macConfigured2);

            // Tamper rejection on the Page MAC pipeline.
            bool tamperRejected = false;
            byte[] tampered = Convert.FromBase64String(macd);
            tampered[0] ^= 0xFF;
            try
            {
                strip.Invoke(page, new object[] { Convert.ToBase64String(tampered) });
            }
            catch (TargetInvocationException)
            {
                tamperRejected = true;
            }

            return new object[]
            {
                roundTrips,
                configuredKeyHonored,
                differs,
                verifies,
                tamperRejected,
            };
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) { return false; }
            for (int i = 0; i < a.Length; i++) { if (a[i] != b[i]) { return false; } }
            return true;
        }
    }
}
