using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Xunit;

namespace System.Web.Tests
{
    // Tier-4 byte-compatibility gate for ObjectStateFormatter.
    //
    // The fixtures under fixtures/viewstate/ were captured from the REAL
    // .NET Framework 4.8 System.Web.UI.ObjectStateFormatter (see manifest.json).
    // Each case carries:
    //   * base64        -- the MAC-FREE OSF output (what OUR Serialize must emit)
    //   * rawHex        -- same bytes in hex (cross-check)
    //   * macdBase64Sha1 / macdBase64HmacSha256
    //                   -- base64( rawBytes || HMAC(validationKey, rawBytes) )
    //
    // We drive OUR ObjectStateFormatter through the SystemWebUnderTest ALC bridge
    // so Serialize/Deserialize bind to our clean-room types (Pair/Triplet/
    // IndexedString are OUR types and must be constructed from the ALC assembly so
    // the formatter's `value.GetType() == typeof(Pair)` checks match).
    //
    // MachineKey wiring: the manifest fixes a canonical validationKey (hex) and a
    // validation algorithm (SHA1). OSF itself does NOT apply the MAC -- the page
    // framework appends HMAC(validationKey, osfBytes). We reproduce that exactly
    // by calling our internal ObjectStateFormatter.ComputeMac(raw, HexToBytes(key),
    // algo) helper and base64-encoding raw||mac, then asserting it equals the
    // captured macdBase64 for BOTH SHA1 and HMACSHA256. That proves our formatter
    // bytes and our MAC pipeline are jointly FW-identical.
    public class ViewStateCompatTests
    {
        private const string OsfTypeName = "System.Web.UI.ObjectStateFormatter";
        private const string PairTypeName = "System.Web.UI.Pair";
        private const string TripletTypeName = "System.Web.UI.Triplet";
        private const string IndexedStringTypeName = "System.Web.UI.IndexedString";

        // Canonical test machineKey from fixtures/viewstate/manifest.json (NOT a secret).
        private const string ValidationKeyHex =
            "1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF" +
            "1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF";

        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        // ----- graph builders that must use OUR (ALC) types -----
        private static object NewOsf()
        {
            return Web.CreateInstance(OsfTypeName);
        }

        private static object NewPair(object a, object b)
        {
            return Activator.CreateInstance(Web.Type(PairTypeName), new object[] { a, b });
        }

        private static object NewTriplet(object a, object b, object c)
        {
            return Activator.CreateInstance(Web.Type(TripletTypeName), new object[] { a, b, c });
        }

        private static object NewIndexedString(string s)
        {
            return Activator.CreateInstance(Web.Type(IndexedStringTypeName), new object[] { s });
        }

        private static string Serialize(object graph)
        {
            object osf = NewOsf();
            return (string)Web.Invoke(osf, "Serialize", graph);
        }

        private static object Deserialize(string base64)
        {
            object osf = NewOsf();
            // Bind the (string) overload explicitly.
            MethodInfo mi = osf.GetType().GetMethod(
                "Deserialize",
                BindingFlags.Public | BindingFlags.Instance,
                null, new Type[] { typeof(string) }, null);
            Assert.True(mi != null, "Deserialize(string) not found");
            return mi.Invoke(osf, new object[] { base64 });
        }

        // ----- fixture access -----
        private static string FixtureDir()
        {
            // Walk up from the test bin dir to the project root, then fixtures/viewstate.
            DirectoryInfo d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null)
            {
                string candidate = Path.Combine(d.FullName, "fixtures", "viewstate");
                if (Directory.Exists(candidate)) { return candidate; }
                d = d.Parent;
            }
            throw new DirectoryNotFoundException("Could not locate fixtures/viewstate from " + AppContext.BaseDirectory);
        }

        private static JsonElement LoadCase(string fileName)
        {
            string path = Path.Combine(FixtureDir(), fileName);
            string json = File.ReadAllText(path);
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                return doc.RootElement.Clone();
            }
        }

        // Byte-level MAC reproduction via our internal helpers (machineKey wiring proof).
        private static string MacBase64(byte[] raw, string algorithm)
        {
            byte[] key = (byte[])Web.InvokeStatic(OsfTypeName, "HexToBytes", ValidationKeyHex);
            byte[] mac = (byte[])Web.InvokeStatic(OsfTypeName, "ComputeMac", raw, key, algorithm);
            byte[] combined = new byte[raw.Length + mac.Length];
            Array.Copy(raw, 0, combined, 0, raw.Length);
            Array.Copy(mac, 0, combined, raw.Length, mac.Length);
            return Convert.ToBase64String(combined);
        }

        // Core assertion driver: serialize byte-compat + MAC + round-trip.
        private static void AssertCase(string fileName, object graph, Func<object, bool> roundTripOk)
        {
            JsonElement c = LoadCase(fileName);
            string expectedBase64 = c.GetProperty("base64").GetString();

            // 1. Serialize must byte-match the captured MAC-free OSF output.
            string actualBase64 = Serialize(graph);
            Assert.Equal(expectedBase64, actualBase64);

            // 2. MAC pipeline (machineKey wiring) must reproduce both captured MACs.
            byte[] raw = Convert.FromBase64String(expectedBase64);
            if (c.TryGetProperty("macdBase64Sha1", out JsonElement m1))
            {
                Assert.Equal(m1.GetString(), MacBase64(raw, "SHA1"));
            }
            if (c.TryGetProperty("macdBase64HmacSha256", out JsonElement m256))
            {
                Assert.Equal(m256.GetString(), MacBase64(raw, "HMACSHA256"));
            }

            // 3. Deserialize(captured) must round-trip to an equal graph.
            object back = Deserialize(expectedBase64);
            Assert.True(roundTripOk(back),
                fileName + ": round-trip mismatch, got " + DescribeForError(back));
        }

        private static string DescribeForError(object o)
        {
            if (o == null) { return "<null>"; }
            return o.GetType().FullName + " = " + Convert.ToString(o, CultureInfo.InvariantCulture);
        }

        // ---- helpers to read OUR Pair/Triplet/IndexedString fields/props back ----
        private static object PairFirst(object p) { return p.GetType().GetField("First").GetValue(p); }
        private static object PairSecond(object p) { return p.GetType().GetField("Second").GetValue(p); }
        private static object TripFirst(object t) { return t.GetType().GetField("First").GetValue(t); }
        private static object TripSecond(object t) { return t.GetType().GetField("Second").GetValue(t); }
        private static object TripThird(object t) { return t.GetType().GetField("Third").GetValue(t); }
        private static string IndexedValue(object s) { return (string)s.GetType().GetProperty("Value").GetValue(s); }

        // =====================  PRIMITIVES  =====================

        [Fact]
        public void Case00_Null()
        {
            AssertCase("case-00-null.json", null, back => back == null);
        }

        [Fact]
        public void Case01_StringHello()
        {
            AssertCase("case-01-string-hello.json", "hello", back => (back as string) == "hello");
        }

        [Fact]
        public void Case02_StringEmpty()
        {
            AssertCase("case-02-string-empty.json", "", back => (back as string) == "");
        }

        [Fact]
        public void Case03_StringUnicode()
        {
            string s = "café×中";
            AssertCase("case-03-string-unicode.json", s, back => (back as string) == s);
        }

        [Fact]
        public void Case04_Int42()
        {
            AssertCase("case-04-int-42.json", 42, back => (back is int) && (int)back == 42);
        }

        [Fact]
        public void Case05_Int0()
        {
            AssertCase("case-05-int-0.json", 0, back => (back is int) && (int)back == 0);
        }

        [Fact]
        public void Case06_IntNeg1()
        {
            AssertCase("case-06-int-neg1.json", -1, back => (back is int) && (int)back == -1);
        }

        [Fact]
        public void Case07_IntMaxValue()
        {
            AssertCase("case-07-int-maxvalue.json", int.MaxValue, back => (back is int) && (int)back == int.MaxValue);
        }

        [Fact]
        public void Case08_BoolTrue()
        {
            AssertCase("case-08-bool-true.json", true, back => (back is bool) && (bool)back);
        }

        [Fact]
        public void Case09_BoolFalse()
        {
            AssertCase("case-09-bool-false.json", false, back => (back is bool) && !(bool)back);
        }

        [Fact]
        public void Case10_Byte200()
        {
            byte v = 200;
            AssertCase("case-10-byte-200.json", v, back => (back is byte) && (byte)back == 200);
        }

        [Fact]
        public void Case11_Short12345()
        {
            short v = 12345;
            AssertCase("case-11-short-12345.json", v, back => (back is short) && (short)back == 12345);
        }

        [Fact]
        public void Case12_Long()
        {
            long v = 9999999999L;
            AssertCase("case-12-long-9999999999.json", v, back => (back is long) && (long)back == 9999999999L);
        }

        [Fact]
        public void Case13_CharA()
        {
            char v = 'A';
            AssertCase("case-13-char-A.json", v, back => (back is char) && (char)back == 'A');
        }

        [Fact]
        public void Case14_Decimal()
        {
            decimal v = 1234.5678m;
            AssertCase("case-14-decimal-1234.5678.json", v, back => (back is decimal) && (decimal)back == 1234.5678m);
        }

        [Fact]
        public void Case15_Double()
        {
            double v = 3.14159;
            AssertCase("case-15-double-3.14159.json", v, back => (back is double) && (double)back == 3.14159);
        }

        [Fact]
        public void Case16_Float()
        {
            float v = 2.5f;
            AssertCase("case-16-float-2.5.json", v, back => (back is float) && (float)back == 2.5f);
        }

        [Fact]
        public void Case17_DateTimeUtc()
        {
            DateTime v = new DateTime(2026, 6, 2, 13, 45, 30, DateTimeKind.Utc);
            AssertCase("case-17-datetime-utc.json", v, back => (back is DateTime) && ((DateTime)back) == v);
        }

        // =====================  STATE TYPES  =====================

        [Fact]
        public void Case18_Pair()
        {
            object g = NewPair("a", "b");
            AssertCase("case-18-pair.json", g,
                back => (string)PairFirst(back) == "a" && (string)PairSecond(back) == "b");
        }

        [Fact]
        public void Case19_PairNulls()
        {
            object g = NewPair(null, null);
            AssertCase("case-19-pair-nulls.json", g,
                back => PairFirst(back) == null && PairSecond(back) == null);
        }

        [Fact]
        public void Case20_Triplet()
        {
            object g = NewTriplet(1, "two", true);
            AssertCase("case-20-triplet.json", g,
                back => (int)TripFirst(back) == 1 && (string)TripSecond(back) == "two" && (bool)TripThird(back));
        }

        [Fact]
        public void Case21_IndexedString()
        {
            object g = NewIndexedString("btn");
            // OSF deserializes an IndexedString token as IndexedString (token 0x1e).
            AssertCase("case-21-indexedstring.json", g, back =>
            {
                if (back == null) { return false; }
                // Round-trip yields an IndexedString with the same value.
                if (back.GetType().FullName == IndexedStringTypeName)
                {
                    return IndexedValue(back) == "btn";
                }
                return false;
            });
        }

        // =====================  COLLECTIONS / ARRAYS  =====================

        [Fact]
        public void Case22_ArrayList()
        {
            ArrayList al = new ArrayList();
            al.Add("x"); al.Add(1); al.Add(true); al.Add(null);
            AssertCase("case-22-arraylist.json", al, back =>
            {
                ArrayList r = back as ArrayList;
                if (r == null || r.Count != 4) { return false; }
                return (string)r[0] == "x" && (int)r[1] == 1 && (bool)r[2] && r[3] == null;
            });
        }

        [Fact]
        public void Case23_Hashtable()
        {
            // Ordering note: OSF enumerates the Hashtable in its own bucket order.
            // The captured bytes encode {k2->2, k1->v1}. Build the same instance and
            // assert byte-equality (the formatter must reproduce that enumeration).
            Hashtable ht = new Hashtable();
            ht["k1"] = "v1";
            ht["k2"] = 2;
            AssertCase("case-23-hashtable.json", ht, back =>
            {
                Hashtable r = back as Hashtable;
                if (r == null || r.Count != 2) { return false; }
                return (string)r["k1"] == "v1" && (int)r["k2"] == 2;
            });
        }

        [Fact]
        public void Case24_ObjectArray()
        {
            object[] arr = new object[] { "s", 5, false, null };
            AssertCase("case-24-object-array.json", arr, back =>
            {
                object[] r = back as object[];
                if (r == null || r.Length != 4) { return false; }
                return (string)r[0] == "s" && (int)r[1] == 5 && !(bool)r[2] && r[3] == null;
            });
        }

        [Fact]
        public void Case25_StringArray()
        {
            string[] arr = new string[] { "alpha", "beta", "gamma" };
            AssertCase("case-25-string-array.json", arr, back =>
            {
                string[] r = back as string[];
                if (r == null || r.Length != 3) { return false; }
                return r[0] == "alpha" && r[1] == "beta" && r[2] == "gamma";
            });
        }

        [Fact]
        public void Case26_IntArray()
        {
            int[] arr = new int[] { 1, 2, 3 };
            AssertCase("case-26-int-array.json", arr, back =>
            {
                int[] r = back as int[];
                if (r == null || r.Length != 3) { return false; }
                return r[0] == 1 && r[1] == 2 && r[2] == 3;
            });
        }

        // =====================  UNIT  =====================
        // Unit is OUR System.Web.UI.WebControls.Unit struct. The captured bytes encode the
        // OSF Unit token (0x1b) followed by an IEEE-754 double (Value) and an Int32 (UnitType),
        // or the empty-unit token (0x1c) for Unit.Empty. These 3 cases were deferred until the
        // Unit type was implemented (Tier 5a); they now prove our OSF Unit path byte-matches FW.
        private const string UnitTypeName = "System.Web.UI.WebControls.Unit";

        private static object NewUnit(double value, string unitTypeMember)
        {
            Type unitType = Web.Type(UnitTypeName);
            Type unitEnum = Web.Type("System.Web.UI.WebControls.UnitType");
            object ut = Enum.Parse(unitEnum, unitTypeMember);
            // Unit(double value, UnitType type)
            return Activator.CreateInstance(unitType, new object[] { value, ut });
        }

        private static bool UnitMatches(object back, double value, string unitTypeMember)
        {
            if (back == null || back.GetType().FullName != UnitTypeName) { return false; }
            double v = System.Convert.ToDouble(back.GetType().GetProperty("Value").GetValue(back));
            object t = back.GetType().GetProperty("Type").GetValue(back);
            return v == value && string.Equals(t.ToString(), unitTypeMember, StringComparison.Ordinal);
        }

        private static bool UnitIsEmpty(object back)
        {
            if (back == null || back.GetType().FullName != UnitTypeName) { return false; }
            return (bool)back.GetType().GetProperty("IsEmpty").GetValue(back);
        }

        [Fact]
        public void Case27_Unit100px()
        {
            object u = NewUnit(100.0, "Pixel");
            AssertCase("case-27-unit-100px.json", u, back => UnitMatches(back, 100.0, "Pixel"));
        }

        [Fact]
        public void Case28_Unit50pct()
        {
            object u = NewUnit(50.0, "Percentage");
            AssertCase("case-28-unit-50pct.json", u, back => UnitMatches(back, 50.0, "Percentage"));
        }

        [Fact]
        public void Case29_UnitEmpty()
        {
            object empty = Web.GetStaticField(UnitTypeName, "Empty");
            AssertCase("case-29-unit-empty.json", empty, back => UnitIsEmpty(back));
        }

        // =====================  COLOR  =====================

        [Fact]
        public void Case30_ColorRed()
        {
            object red = global::System.Drawing.Color.Red;
            AssertCase("case-30-color-red.json", red, back =>
            {
                if (!(back is global::System.Drawing.Color)) { return false; }
                return ((global::System.Drawing.Color)back).ToArgb() == global::System.Drawing.Color.Red.ToArgb();
            });
        }

        [Fact]
        public void Case31_ColorArgb()
        {
            object c = global::System.Drawing.Color.FromArgb(10, 20, 30, 40);
            AssertCase("case-31-color-argb.json", c, back =>
            {
                if (!(back is global::System.Drawing.Color)) { return false; }
                return ((global::System.Drawing.Color)back).ToArgb()
                    == global::System.Drawing.Color.FromArgb(10, 20, 30, 40).ToArgb();
            });
        }

        // =====================  NESTED / CONTROL TREE  =====================

        [Fact]
        public void Case32_PairNested()
        {
            object inner = NewPair("a", "b");
            object trip = NewTriplet(1, 2, 3);
            object g = NewPair(inner, trip);
            AssertCase("case-32-pair-nested.json", g, back =>
            {
                object f = PairFirst(back);
                object s = PairSecond(back);
                if ((string)PairFirst(f) != "a" || (string)PairSecond(f) != "b") { return false; }
                return (int)TripFirst(s) == 1 && (int)TripSecond(s) == 2 && (int)TripThird(s) == 3;
            });
        }

        [Fact]
        public void Case33_ControlTree()
        {
            // Pair( Triplet("ctrlState", object[]{"Page",1}, null),
            //       ArrayList[ Pair("child0", Pair("Text","Hello")),
            //                  Pair("child1", Pair("Visible", true)) ] )
            object trip = NewTriplet("ctrlState", new object[] { "Page", 1 }, null);
            ArrayList children = new ArrayList();
            children.Add(NewPair("child0", NewPair("Text", "Hello")));
            children.Add(NewPair("child1", NewPair("Visible", true)));
            object g = NewPair(trip, children);

            AssertCase("case-33-control-tree.json", g, back =>
            {
                object first = PairFirst(back);   // Triplet
                object second = PairSecond(back); // ArrayList
                if ((string)TripFirst(first) != "ctrlState") { return false; }
                object[] meta = TripSecond(first) as object[];
                if (meta == null || (string)meta[0] != "Page" || (int)meta[1] != 1) { return false; }
                if (TripThird(first) != null) { return false; }
                ArrayList kids = second as ArrayList;
                if (kids == null || kids.Count != 2) { return false; }
                object c0 = kids[0];
                if ((string)PairFirst(c0) != "child0") { return false; }
                object c0inner = PairSecond(c0);
                if ((string)PairFirst(c0inner) != "Text" || (string)PairSecond(c0inner) != "Hello") { return false; }
                object c1 = kids[1];
                if ((string)PairFirst(c1) != "child1") { return false; }
                object c1inner = PairSecond(c1);
                if ((string)PairFirst(c1inner) != "Visible" || !(bool)PairSecond(c1inner)) { return false; }
                return true;
            });
        }
    }
}
