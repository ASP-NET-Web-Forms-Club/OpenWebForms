using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web;

namespace System.Web.Tests
{
    // Shared fake/in-memory HttpWorkerRequest implementations used by the integration
    // tests. These are referenced by the worker types that execute INSIDE the custom
    // AssemblyLoadContext (via SystemWebUnderTest.RunInAlc), so that the base type
    // System.Web.HttpWorkerRequest binds to OUR clean-room assembly.
    //
    // FakeWorkerRequest models an inbound request: a verb, URL/query, headers and an
    // entity body fully preloaded in memory. It is enough to drive HttpRequest's
    // QueryString/Form/Files/Cookies/Headers surfaces (which delegate parsing to the
    // vendored cshttp parsers).
    internal sealed class FakeWorkerRequest : HttpWorkerRequest
    {
        private readonly string _verb;
        private readonly string _uriPath;
        private readonly string _queryString;
        private readonly Dictionary<string, string> _headers;
        private readonly byte[] _body;

        public FakeWorkerRequest(
            string verb,
            string uriPath,
            string queryString,
            Dictionary<string, string> headers,
            byte[] body)
        {
            _verb = verb ?? "GET";
            _uriPath = string.IsNullOrEmpty(uriPath) ? "/" : uriPath;
            _queryString = queryString ?? string.Empty;
            _headers = headers ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _body = body ?? Array.Empty<byte>();
        }

        // ----- Request line -----
        public override string GetUriPath() { return _uriPath; }
        public override string GetQueryString() { return _queryString; }
        public override string GetRawUrl()
        {
            return string.IsNullOrEmpty(_queryString) ? _uriPath : _uriPath + "?" + _queryString;
        }
        public override string GetHttpVerbName() { return _verb; }
        public override string GetHttpVersion() { return "HTTP/1.1"; }
        public override string GetRemoteAddress() { return "127.0.0.1"; }
        public override int GetRemotePort() { return 12345; }
        public override string GetLocalAddress() { return "127.0.0.1"; }
        public override int GetLocalPort() { return 80; }
        public override bool IsSecure() { return false; }
        public override string GetServerName() { return "localhost"; }
        public override string GetAppPath() { return "/"; }
        public override string GetAppPathTranslated() { return AppContext.BaseDirectory; }
        public override string GetFilePath() { return _uriPath; }

        public override string MapPath(string virtualPath)
        {
            string rel = virtualPath ?? string.Empty;
            if (rel.StartsWith("/", StringComparison.Ordinal)) rel = rel.Substring(1);
            rel = rel.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(AppContext.BaseDirectory, rel);
        }

        // ----- Headers -----
        public override string GetUnknownRequestHeader(string name)
        {
            string v;
            return _headers.TryGetValue(name, out v) ? v : null;
        }
        public override string[][] GetUnknownRequestHeaders()
        {
            string[][] result = new string[_headers.Count][];
            int i = 0;
            foreach (KeyValuePair<string, string> kvp in _headers)
            {
                result[i] = new string[] { kvp.Key, kvp.Value };
                i++;
            }
            return result;
        }
        public override string GetKnownRequestHeader(int index)
        {
            string name = GetKnownRequestHeaderName(index);
            if (string.IsNullOrEmpty(name)) return null;
            return GetUnknownRequestHeader(name);
        }
        public override string GetServerVariable(string name)
        {
            switch (name)
            {
                case "REQUEST_METHOD": return _verb;
                case "QUERY_STRING": return _queryString;
                case "REMOTE_ADDR": return "127.0.0.1";
                case "SERVER_PROTOCOL": return "HTTP/1.1";
                default:
                    string v;
                    return _headers.TryGetValue(name, out v) ? v : string.Empty;
            }
        }

        // ----- Entity body (fully preloaded) -----
        public override byte[] GetPreloadedEntityBody() { return _body; }
        public override bool IsEntireEntityBodyIsPreloaded() { return true; }
        public override int GetTotalEntityBodyLength() { return _body.Length; }
        public override int ReadEntityBody(byte[] buffer, int size) { return 0; }
        public override int ReadEntityBody(byte[] buffer, int offset, int size) { return 0; }

        // ----- Response sinks (no-op; this worker is input-only) -----
        public override void SendStatus(int statusCode, string statusDescription) { }
        public override void SendKnownResponseHeader(int index, string value) { }
        public override void SendUnknownResponseHeader(string name, string value) { }
        public override void SendResponseFromMemory(byte[] data, int length) { }
        public override void SendResponseFromFile(string filename, long offset, long length) { }
        public override void SendResponseFromFile(IntPtr handle, long offset, long length) { }
        public override void FlushResponse(bool finalFlush) { }
        public override void EndOfRequest() { }
        public override bool IsClientConnected() { return true; }
    }

    // CapturingWorkerRequest records everything the response pipeline pushes at it:
    // the status line, headers (in order), and the raw response body bytes. Used by the
    // response and pipeline tests to assert exactly what bytes/headers reach the wire.
    internal sealed class CapturingWorkerRequest : HttpWorkerRequest
    {
        private readonly string _verb;
        private readonly string _uriPath;
        private readonly string _queryString;
        private readonly Dictionary<string, string> _reqHeaders;
        private readonly byte[] _reqBody;

        private readonly MemoryStream _responseBody = new MemoryStream();
        private readonly List<string[]> _responseHeaders = new List<string[]>();
        private int _statusCode = 200;
        private string _statusDescription = "OK";
        private bool _ended;
        private bool _flushed;

        public CapturingWorkerRequest(
            string verb,
            string uriPath,
            string queryString,
            Dictionary<string, string> reqHeaders,
            byte[] reqBody)
        {
            _verb = verb ?? "GET";
            _uriPath = string.IsNullOrEmpty(uriPath) ? "/" : uriPath;
            _queryString = queryString ?? string.Empty;
            _reqHeaders = reqHeaders ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _reqBody = reqBody ?? Array.Empty<byte>();
        }

        public int CapturedStatus { get { return _statusCode; } }
        public string CapturedStatusDescription { get { return _statusDescription; } }
        public byte[] CapturedBody { get { return _responseBody.ToArray(); } }
        public bool Ended { get { return _ended; } }
        public bool Flushed { get { return _flushed; } }

        // Returns the first response header value matching name (case-insensitive), or null.
        public string GetCapturedHeader(string name)
        {
            for (int i = 0; i < _responseHeaders.Count; i++)
            {
                if (string.Equals(_responseHeaders[i][0], name, StringComparison.OrdinalIgnoreCase))
                {
                    return _responseHeaders[i][1];
                }
            }
            return null;
        }
        // Returns ALL response header values matching name (e.g. multiple Set-Cookie).
        public List<string> GetCapturedHeaders(string name)
        {
            List<string> result = new List<string>();
            for (int i = 0; i < _responseHeaders.Count; i++)
            {
                if (string.Equals(_responseHeaders[i][0], name, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(_responseHeaders[i][1]);
                }
            }
            return result;
        }

        // ----- Request line -----
        public override string GetUriPath() { return _uriPath; }
        public override string GetQueryString() { return _queryString; }
        public override string GetRawUrl()
        {
            return string.IsNullOrEmpty(_queryString) ? _uriPath : _uriPath + "?" + _queryString;
        }
        public override string GetHttpVerbName() { return _verb; }
        public override string GetHttpVersion() { return "HTTP/1.1"; }
        public override string GetRemoteAddress() { return "127.0.0.1"; }
        public override int GetRemotePort() { return 12345; }
        public override string GetLocalAddress() { return "127.0.0.1"; }
        public override int GetLocalPort() { return 80; }
        public override bool IsSecure() { return false; }
        public override string GetServerName() { return "localhost"; }
        public override string GetAppPath() { return "/"; }
        public override string GetAppPathTranslated() { return AppContext.BaseDirectory; }
        public override string GetFilePath() { return _uriPath; }
        public override string MapPath(string virtualPath)
        {
            string rel = virtualPath ?? string.Empty;
            if (rel.StartsWith("/", StringComparison.Ordinal)) rel = rel.Substring(1);
            rel = rel.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(AppContext.BaseDirectory, rel);
        }

        // ----- Request headers/body -----
        public override string GetUnknownRequestHeader(string name)
        {
            string v;
            return _reqHeaders.TryGetValue(name, out v) ? v : null;
        }
        public override string[][] GetUnknownRequestHeaders()
        {
            string[][] result = new string[_reqHeaders.Count][];
            int i = 0;
            foreach (KeyValuePair<string, string> kvp in _reqHeaders)
            {
                result[i] = new string[] { kvp.Key, kvp.Value };
                i++;
            }
            return result;
        }
        public override byte[] GetPreloadedEntityBody() { return _reqBody; }
        public override bool IsEntireEntityBodyIsPreloaded() { return true; }
        public override int ReadEntityBody(byte[] buffer, int size) { return 0; }
        public override int ReadEntityBody(byte[] buffer, int offset, int size) { return 0; }

        // ----- Response sinks (capture) -----
        public override void SendStatus(int statusCode, string statusDescription)
        {
            _statusCode = statusCode;
            _statusDescription = statusDescription;
        }
        public override void SendKnownResponseHeader(int index, string value)
        {
            string name = GetKnownResponseHeaderName(index);
            if (!string.IsNullOrEmpty(name))
            {
                _responseHeaders.Add(new string[] { name, value });
            }
        }
        public override void SendUnknownResponseHeader(string name, string value)
        {
            _responseHeaders.Add(new string[] { name, value });
        }
        public override void SendResponseFromMemory(byte[] data, int length)
        {
            if (data != null && length > 0)
            {
                _responseBody.Write(data, 0, length);
            }
        }
        public override void SendResponseFromFile(string filename, long offset, long length) { }
        public override void SendResponseFromFile(IntPtr handle, long offset, long length) { }
        public override void FlushResponse(bool finalFlush) { _flushed = true; }
        public override void EndOfRequest() { _ended = true; }
        public override bool IsClientConnected() { return true; }
    }

    // A worker request that HONORS the first Content-Length response header it is told, exactly like
    // System.Net.HttpListener (which truncates the body to ContentLength64). This makes it possible
    // to catch the live-host regression where a premature flush (e.g. a StreamWriter BOM preamble +
    // AutoFlush) locks in a too-small Content-Length and truncates the rendered page to a few bytes.
    // A plain capturing worker that appends every SendResponseFromMemory call cannot detect that.
    internal sealed class ContentLengthHonoringWorkerRequest : HttpWorkerRequest
    {
        private readonly string _verb;
        private readonly string _uriPath;
        private readonly string _queryString;
        private readonly Dictionary<string, string> _reqHeaders;
        private readonly byte[] _reqBody;

        private readonly MemoryStream _responseBody = new MemoryStream();
        private int _statusCode = 200;
        private string _statusDescription = "OK";
        private long _declaredContentLength = -1;
        private long _accepted;

        public ContentLengthHonoringWorkerRequest(
            string verb, string uriPath, string queryString,
            Dictionary<string, string> reqHeaders, byte[] reqBody)
        {
            _verb = verb ?? "GET";
            _uriPath = string.IsNullOrEmpty(uriPath) ? "/" : uriPath;
            _queryString = queryString ?? string.Empty;
            _reqHeaders = reqHeaders ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _reqBody = reqBody ?? Array.Empty<byte>();
        }

        public int CapturedStatus { get { return _statusCode; } }
        public byte[] CapturedBody { get { return _responseBody.ToArray(); } }
        public long DeclaredContentLength { get { return _declaredContentLength; } }

        public override string GetUriPath() { return _uriPath; }
        public override string GetQueryString() { return _queryString; }
        public override string GetRawUrl()
        {
            return string.IsNullOrEmpty(_queryString) ? _uriPath : _uriPath + "?" + _queryString;
        }
        public override string GetHttpVerbName() { return _verb; }
        public override string GetHttpVersion() { return "HTTP/1.1"; }
        public override string GetRemoteAddress() { return "127.0.0.1"; }
        public override int GetRemotePort() { return 12345; }
        public override string GetLocalAddress() { return "127.0.0.1"; }
        public override int GetLocalPort() { return 80; }
        public override bool IsSecure() { return false; }
        public override string GetServerName() { return "localhost"; }
        public override string GetAppPath() { return "/"; }
        public override string GetAppPathTranslated() { return AppContext.BaseDirectory; }
        public override string GetFilePath() { return _uriPath; }
        public override string MapPath(string virtualPath)
        {
            string rel = virtualPath ?? string.Empty;
            if (rel.StartsWith("/", StringComparison.Ordinal)) rel = rel.Substring(1);
            rel = rel.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(AppContext.BaseDirectory, rel);
        }

        public override string GetUnknownRequestHeader(string name)
        {
            string v;
            return _reqHeaders.TryGetValue(name, out v) ? v : null;
        }
        public override string[][] GetUnknownRequestHeaders()
        {
            string[][] result = new string[_reqHeaders.Count][];
            int i = 0;
            foreach (KeyValuePair<string, string> kvp in _reqHeaders)
            {
                result[i] = new string[] { kvp.Key, kvp.Value };
                i++;
            }
            return result;
        }
        public override byte[] GetPreloadedEntityBody() { return _reqBody; }
        public override bool IsEntireEntityBodyIsPreloaded() { return true; }
        public override int ReadEntityBody(byte[] buffer, int size) { return 0; }
        public override int ReadEntityBody(byte[] buffer, int offset, int size) { return 0; }

        public override void SendStatus(int statusCode, string statusDescription)
        {
            _statusCode = statusCode;
            _statusDescription = statusDescription;
        }
        public override void SendKnownResponseHeader(int index, string value)
        {
            string name = GetKnownResponseHeaderName(index);
            HonorContentLength(name, value);
        }
        public override void SendUnknownResponseHeader(string name, string value)
        {
            HonorContentLength(name, value);
        }
        private void HonorContentLength(string name, string value)
        {
            if (_declaredContentLength < 0 &&
                string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                long len;
                if (long.TryParse(value, out len)) { _declaredContentLength = len; }
            }
        }
        public override void SendResponseFromMemory(byte[] data, int length)
        {
            if (data == null || length <= 0) { return; }
            // Mimic HttpListener: never write more than the declared Content-Length.
            long allowed = length;
            if (_declaredContentLength >= 0)
            {
                long remaining = _declaredContentLength - _accepted;
                if (remaining <= 0) { return; }
                if (allowed > remaining) { allowed = remaining; }
            }
            _responseBody.Write(data, 0, (int)allowed);
            _accepted += allowed;
        }
        public override void SendResponseFromFile(string filename, long offset, long length) { }
        public override void SendResponseFromFile(IntPtr handle, long offset, long length) { }
        public override void FlushResponse(bool finalFlush) { }
        public override void EndOfRequest() { }
        public override bool IsClientConnected() { return true; }
    }
}
