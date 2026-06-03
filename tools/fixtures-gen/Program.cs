using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;

// fixtures-gen (net48): captures byte-exact ground-truth __VIEWSTATE output from the
// REAL .NET Framework System.Web.UI.ObjectStateFormatter / LosFormatter, plus the
// legacy ViewState MAC computed under a FIXED, KNOWN machineKey (deterministic).
//
// CLEAN-ROOM NOTE: this only observes the serialized OUTPUT BYTES (a data-format
// reverse-engineer). No Microsoft source/IL is read or copied.

internal static class Program
{
    // FIXED canonical keys (duplicated verbatim in app.config + manifest).
    // 64 bytes validation key (128 hex chars), 24 bytes decryption key (48 hex chars).
    private const string ValidationKeyHex =
        "1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF" +
        "1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF";
    private const string DecryptionKeyHex =
        "1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF";

    private static byte[] ValidationKey => HexToBytes(ValidationKeyHex);

    private static int _idx = 0;

    private static void Main(string[] outArgs)
    {
        string outDir = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..",
            "fixtures", "viewstate"));
        if (outArgs != null && outArgs.Length > 0 && !string.IsNullOrEmpty(outArgs[0]))
            outDir = Path.GetFullPath(outArgs[0]);
        Directory.CreateDirectory(outDir);

        var cases = new List<Case>();

        // ---- Primitives & nulls ----
        Add(cases, "null", "null reference", null);
        Add(cases, "string-hello", "System.String \"hello\"", "hello");
        Add(cases, "string-empty", "System.String empty", "");
        Add(cases, "string-unicode", "System.String unicode \"café×中\"", "café×中");
        Add(cases, "int-42", "System.Int32 42", 42);
        Add(cases, "int-0", "System.Int32 0", 0);
        Add(cases, "int-neg1", "System.Int32 -1", -1);
        Add(cases, "int-maxvalue", "System.Int32 Int32.MaxValue", int.MaxValue);
        Add(cases, "bool-true", "System.Boolean true", true);
        Add(cases, "bool-false", "System.Boolean false", false);
        Add(cases, "byte-200", "System.Byte 200", (byte)200);
        Add(cases, "short-12345", "System.Int16 12345", (short)12345);
        Add(cases, "long-9999999999", "System.Int64 9999999999", 9999999999L);
        Add(cases, "char-A", "System.Char 'A'", 'A');
        Add(cases, "decimal-1234.5678", "System.Decimal 1234.5678", 1234.5678m);
        Add(cases, "double-3.14159", "System.Double 3.14159", 3.14159d);
        Add(cases, "float-2.5", "System.Single 2.5", 2.5f);
        Add(cases, "datetime-utc", "System.DateTime 2026-06-02T13:45:30Z (Utc)",
            new DateTime(2026, 6, 2, 13, 45, 30, DateTimeKind.Utc));

        // ---- ViewState special types ----
        Add(cases, "pair", "System.Web.UI.Pair(\"a\",\"b\")", new Pair("a", "b"));
        Add(cases, "pair-nulls", "System.Web.UI.Pair(null,null)", new Pair(null, null));
        Add(cases, "triplet", "System.Web.UI.Triplet(1,\"two\",true)", new Triplet(1, "two", true));
        Add(cases, "indexedstring", "System.Web.UI.IndexedString \"btn\"", new IndexedString("btn"));

        // ---- Collections ----
        var al = new ArrayList { "x", 1, true, null };
        Add(cases, "arraylist", "System.Collections.ArrayList [\"x\",1,true,null]", al);

        var ht = new Hashtable();
        ht["k1"] = "v1";
        ht["k2"] = 2;
        Add(cases, "hashtable", "System.Collections.Hashtable {k1:v1,k2:2}", ht);

        var objArr = new object[] { "s", 5, false, null };
        Add(cases, "object-array", "System.Object[] {\"s\",5,false,null}", objArr);

        var strArr = new string[] { "alpha", "beta", "gamma" };
        Add(cases, "string-array", "System.String[] {alpha,beta,gamma}", strArr);

        var intArr = new int[] { 1, 2, 3 };
        Add(cases, "int-array", "System.Int32[] {1,2,3}", intArr);

        // ---- WebControls.Unit & Drawing.Color ----
        Add(cases, "unit-100px", "System.Web.UI.WebControls.Unit 100px", new Unit(100, UnitType.Pixel));
        Add(cases, "unit-50pct", "System.Web.UI.WebControls.Unit 50%", new Unit(50, UnitType.Percentage));
        Add(cases, "unit-empty", "System.Web.UI.WebControls.Unit Empty", Unit.Empty);
        Add(cases, "color-red", "System.Drawing.Color Red (known)", System.Drawing.Color.Red);
        Add(cases, "color-argb", "System.Drawing.Color FromArgb(10,20,30,40)",
            System.Drawing.Color.FromArgb(10, 20, 30, 40));

        // ---- Nesting: Pair/Triplet ----
        Add(cases, "pair-nested", "Pair(Pair(\"a\",\"b\"), Triplet(1,2,3))",
            new Pair(new Pair("a", "b"), new Triplet(1, 2, 3)));

        // ---- Typical control-tree ViewState shape ----
        // Pair( Triplet(controlState, viewState, adapter) , ArrayList of child pairs )
        var children = new ArrayList
        {
            new Pair("child0", new Pair("Text", "Hello")),
            new Pair("child1", new Pair("Visible", true)),
        };
        var controlTree = new Pair(
            new Triplet("ctrlState", new object[] { "Page", 1 }, null),
            children);
        Add(cases, "control-tree",
            "Typical control-tree: Pair(Triplet(...), ArrayList of child Pairs)", controlTree);

        // ---- Build the manifest from this run; validation algo comes from the
        //      active app.config so SHA1 vs HMACSHA256 runs produce distinct manifests. ----
        string algoFromConfig = GetConfiguredValidationAlgo();
        WriteManifest(outDir, cases, algoFromConfig, algoFromConfig);

        // Per-case JSON files
        foreach (var c in cases)
            WriteCaseFile(outDir, c, "SHA1");

        Console.WriteLine("Wrote {0} cases to {1}", cases.Count, outDir);
        Console.WriteLine("Configured validation algorithm (from app.config): {0}", algoFromConfig);

        // Sanity: print a couple
        foreach (var c in cases.Take(3))
            Console.WriteLine("  {0}: rawHex={1} base64={2}", c.Name, c.RawHex, c.Base64);
    }

    // ---- Capture one case ----
    private static void Add(List<Case> list, string name, string clrDesc, object graph)
    {
        var c = new Case { Index = _idx++, Name = name, ClrTypeDescription = clrDesc };

        var osf = new ObjectStateFormatter();
        // base64 (deterministic, no MAC at the parameterless-ctor level)
        c.Base64 = osf.Serialize(graph);

        // raw pre-base64 bytes via Serialize(Stream, obj)
        using (var ms = new MemoryStream())
        {
            osf.Serialize(ms, graph);
            byte[] raw = ms.ToArray();
            c.RawHex = BytesToHex(raw);
            c.RawLength = raw.Length;

            // Legacy ViewState MAC over the raw bytes with the FIXED validationKey.
            // SHA1 validation => HMACSHA1 ; HMACSHA256 => HMACSHA256.
            c.MacSha1Hex = BytesToHex(HmacSha1(raw));
            c.MacHmacSha256Hex = BytesToHex(HmacSha256(raw));
            // The MAC'd __VIEWSTATE string = base64( rawBytes || mac )
            c.MacdBase64Sha1 = Convert.ToBase64String(Concat(raw, HmacSha1(raw)));
            c.MacdBase64HmacSha256 = Convert.ToBase64String(Concat(raw, HmacSha256(raw)));
        }
        list.Add(c);
    }

    private static byte[] HmacSha1(byte[] data)
    {
        using (var h = new HMACSHA1(ValidationKey)) return h.ComputeHash(data);
    }
    private static byte[] HmacSha256(byte[] data)
    {
        using (var h = new HMACSHA256(ValidationKey)) return h.ComputeHash(data);
    }
    private static byte[] Concat(byte[] a, byte[] b)
    {
        var r = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, r, 0, a.Length);
        Buffer.BlockCopy(b, 0, r, a.Length, b.Length);
        return r;
    }

    private static string GetConfiguredValidationAlgo()
    {
        try
        {
            var doc = new System.Xml.XmlDocument();
            string cfgPath = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            doc.Load(cfgPath);
            var node = doc.SelectSingleNode("/configuration/system.web/machineKey");
            if (node != null && node.Attributes["validation"] != null)
                return node.Attributes["validation"].Value;
        }
        catch (Exception ex) { return "SHA1 (default; config read failed: " + ex.Message + ")"; }
        return "SHA1";
    }

    // ---- Capture LosFormatter for a couple cases (separate, into manifest) ----
    private static string LosBase64(object graph)
    {
        var los = new LosFormatter();
        using (var sw = new StringWriter())
        {
            los.Serialize(sw, graph);
            return sw.ToString();
        }
    }

    // ---- JSON emit (manual, no external deps) ----
    private static void WriteManifest(string dir, List<Case> cases, string algoLabel, string algoFromConfig)
    {
        string manifestName = "manifest.json";
        if (!string.IsNullOrEmpty(algoLabel) &&
            algoLabel.IndexOf("HMACSHA256", StringComparison.OrdinalIgnoreCase) >= 0)
            manifestName = "manifest-hmacsha256.json";
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"description\": \"Ground-truth __VIEWSTATE wire-format fixtures captured from REAL .NET Framework 4.8 System.Web.UI.ObjectStateFormatter (net48 exe, GAC System.Web). Raw bytes are MAC-free OSF output; mac* fields are the legacy ViewState HMAC computed over the raw bytes with the fixed validationKey.\",");
        sb.AppendLine("  \"capturedAtUtc\": \"" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + "\",");
        sb.AppendLine("  \"runtime\": \"" + JsonEsc(Environment.Version.ToString()) + "\",");
        sb.AppendLine("  \"systemWebAssembly\": \"" + JsonEsc(typeof(ObjectStateFormatter).Assembly.FullName) + "\",");
        sb.AppendLine("  \"systemWebLocation\": \"" + JsonEsc(typeof(ObjectStateFormatter).Assembly.Location) + "\",");
        sb.AppendLine("  \"machineKey\": {");
        sb.AppendLine("    \"validationKey\": \"" + ValidationKeyHex + "\",");
        sb.AppendLine("    \"decryptionKey\": \"" + DecryptionKeyHex + "\",");
        sb.AppendLine("    \"validation\": \"" + JsonEsc(algoFromConfig) + "\",");
        sb.AppendLine("    \"decryption\": \"AES\",");
        sb.AppendLine("    \"note\": \"Keys are fixed canonical test values, NOT secrets. mac fields provide both SHA1(=HMACSHA1) and HMACSHA256 so either configured algorithm is reproducible.\"");
        sb.AppendLine("  },");
        sb.AppendLine("  \"wireFormatNotes\": {");
        sb.AppendLine("    \"prefixByte\": \"0xFF marker followed by 0x01 version, then a token-tagged graph\",");
        sb.AppendLine("    \"macLayout\": \"__VIEWSTATE (MAC enabled) = base64( osfRawBytes || HMAC(validationKey, osfRawBytes) ). HMACSHA1=20 bytes, HMACSHA256=32 bytes.\"");
        sb.AppendLine("  },");

        // LosFormatter samples
        sb.AppendLine("  \"losFormatter\": [");
        var losSamples = new (string, string, object)[]
        {
            ("los-string-hello", "System.String \"hello\"", "hello"),
            ("los-pair", "System.Web.UI.Pair(\"a\",\"b\")", new Pair("a", "b")),
        };
        for (int i = 0; i < losSamples.Length; i++)
        {
            var (n, d, g) = losSamples[i];
            sb.Append("    { \"name\": \"" + n + "\", \"clrTypeDescription\": \"" + JsonEsc(d) +
                      "\", \"base64\": \"" + JsonEsc(LosBase64(g)) + "\" }");
            sb.AppendLine(i < losSamples.Length - 1 ? "," : "");
        }
        sb.AppendLine("  ],");

        sb.AppendLine("  \"cases\": [");
        for (int i = 0; i < cases.Count; i++)
        {
            sb.Append(CaseJson(cases[i], "    "));
            sb.AppendLine(i < cases.Count - 1 ? "," : "");
        }
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        File.WriteAllText(Path.Combine(dir, manifestName), sb.ToString(), new UTF8Encoding(false));
    }

    private static void WriteCaseFile(string dir, Case c, string algoLabel)
    {
        string fn = string.Format(CultureInfo.InvariantCulture, "case-{0:D2}-{1}.json", c.Index, c.Name);
        File.WriteAllText(Path.Combine(dir, fn), CaseJson(c, ""), new UTF8Encoding(false));
    }

    private static string CaseJson(Case c, string indent)
    {
        var sb = new StringBuilder();
        sb.AppendLine(indent + "{");
        sb.AppendLine(indent + "  \"index\": " + c.Index + ",");
        sb.AppendLine(indent + "  \"name\": \"" + JsonEsc(c.Name) + "\",");
        sb.AppendLine(indent + "  \"clrTypeDescription\": \"" + JsonEsc(c.ClrTypeDescription) + "\",");
        sb.AppendLine(indent + "  \"base64\": \"" + JsonEsc(c.Base64) + "\",");
        sb.AppendLine(indent + "  \"rawHex\": \"" + JsonEsc(c.RawHex) + "\",");
        sb.AppendLine(indent + "  \"rawLength\": " + c.RawLength + ",");
        sb.AppendLine(indent + "  \"macSha1Hex\": \"" + JsonEsc(c.MacSha1Hex) + "\",");
        sb.AppendLine(indent + "  \"macHmacSha256Hex\": \"" + JsonEsc(c.MacHmacSha256Hex) + "\",");
        sb.AppendLine(indent + "  \"macdBase64Sha1\": \"" + JsonEsc(c.MacdBase64Sha1) + "\",");
        sb.Append(indent + "  \"macdBase64HmacSha256\": \"" + JsonEsc(c.MacdBase64HmacSha256) + "\"");
        sb.AppendLine();
        sb.Append(indent + "}");
        return sb.ToString();
    }

    private static string JsonEsc(string s)
    {
        if (s == null) return "";
        var sb = new StringBuilder(s.Length + 8);
        foreach (char ch in s)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (ch < 0x20) sb.Append("\\u").Append(((int)ch).ToString("x4"));
                    else sb.Append(ch);
                    break;
            }
        }
        return sb.ToString();
    }

    private static string BytesToHex(byte[] b)
    {
        if (b == null) return "";
        var sb = new StringBuilder(b.Length * 2);
        for (int i = 0; i < b.Length; i++) sb.Append(b[i].ToString("x2"));
        return sb.ToString();
    }

    private static byte[] HexToBytes(string hex)
    {
        var r = new byte[hex.Length / 2];
        for (int i = 0; i < r.Length; i++)
            r[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return r;
    }

    private sealed class Case
    {
        public int Index;
        public string Name;
        public string ClrTypeDescription;
        public string Base64;
        public string RawHex;
        public int RawLength;
        public string MacSha1Hex;
        public string MacHmacSha256Hex;
        public string MacdBase64Sha1;
        public string MacdBase64HmacSha256;
    }
}
