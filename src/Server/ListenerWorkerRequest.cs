// Clean-room standalone host: HttpWorkerRequest backed by System.Net.HttpListenerContext.
// This type is NEW (not part of the frozen public skeleton); its method bodies are real code.
// Modeled on the shape of System.Web.Hosting.SimpleWorkerRequest.
using System;
using System.IO;
using System.Net;
using System.Text;

namespace System.Web.Server
{
    internal sealed class ListenerWorkerRequest : global::System.Web.HttpWorkerRequest
    {
        private readonly HttpListenerContext _context;
        private readonly HttpListenerRequest _request;
        private readonly HttpListenerResponse _response;
        private readonly string _physicalDir;
        private readonly string _virtualDir;
        private readonly Stream _outputStream;
        private bool _headersSent;

        public ListenerWorkerRequest(HttpListenerContext context, string physicalDir, string virtualDir)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }
            _context = context;
            _request = context.Request;
            _response = context.Response;
            _physicalDir = physicalDir == null ? string.Empty : physicalDir;
            _virtualDir = string.IsNullOrEmpty(virtualDir) ? "/" : virtualDir;
            _outputStream = _response.OutputStream;
        }

        // ----- Request line / URL getters (abstract) -----

        public override string GetUriPath()
        {
            return _request.Url == null ? "/" : _request.Url.AbsolutePath;
        }

        public override string GetQueryString()
        {
            string raw = _request.Url == null ? null : _request.Url.Query;
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }
            // HttpListener leaves the leading '?' in Query; strip it for parity with IIS workers.
            if (raw[0] == '?')
            {
                return raw.Substring(1);
            }
            return raw;
        }

        public override string GetRawUrl()
        {
            return _request.RawUrl == null ? "/" : _request.RawUrl;
        }

        public override string GetHttpVerbName()
        {
            return _request.HttpMethod == null ? "GET" : _request.HttpMethod;
        }

        public override string GetHttpVersion()
        {
            Version v = _request.ProtocolVersion;
            if (v == null)
            {
                return "HTTP/1.1";
            }
            return "HTTP/" + v.Major + "." + v.Minor;
        }

        public override string GetRemoteAddress()
        {
            IPEndPoint ep = _request.RemoteEndPoint;
            return ep == null ? string.Empty : ep.Address.ToString();
        }

        public override int GetRemotePort()
        {
            IPEndPoint ep = _request.RemoteEndPoint;
            return ep == null ? 0 : ep.Port;
        }

        public override string GetLocalAddress()
        {
            IPEndPoint ep = _request.LocalEndPoint;
            return ep == null ? string.Empty : ep.Address.ToString();
        }

        public override int GetLocalPort()
        {
            IPEndPoint ep = _request.LocalEndPoint;
            return ep == null ? 0 : ep.Port;
        }

        public override bool IsSecure()
        {
            return _request.IsSecureConnection;
        }

        public override string GetProtocol()
        {
            return IsSecure() ? "https" : "http";
        }

        // ----- App / file path helpers (useful virtuals) -----

        public override string GetAppPath()
        {
            return _virtualDir;
        }

        public override string GetAppPathTranslated()
        {
            return _physicalDir;
        }

        public override string GetFilePath()
        {
            return GetUriPath();
        }

        public override string GetFilePathTranslated()
        {
            return MapPath(GetUriPath());
        }

        public override string MapPath(string virtualPath)
        {
            if (string.IsNullOrEmpty(virtualPath))
            {
                return _physicalDir;
            }
            string rel = virtualPath;
            if (rel.StartsWith(_virtualDir, StringComparison.OrdinalIgnoreCase) && _virtualDir.Length > 1)
            {
                rel = rel.Substring(_virtualDir.Length);
            }
            rel = rel.Replace('/', Path.DirectorySeparatorChar);
            if (rel.Length > 0 && rel[0] == Path.DirectorySeparatorChar)
            {
                rel = rel.Substring(1);
            }
            if (rel.Length == 0)
            {
                return _physicalDir;
            }
            return Path.Combine(_physicalDir, rel);
        }

        // ----- Request header getters (useful virtuals) -----

        public override string GetKnownRequestHeader(int index)
        {
            string name = global::System.Web.HttpWorkerRequest.GetKnownRequestHeaderName(index);
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            return _request.Headers[name];
        }

        public override string GetUnknownRequestHeader(string name)
        {
            return _request.Headers[name];
        }

        public override string[][] GetUnknownRequestHeaders()
        {
            System.Collections.Specialized.NameValueCollection headers = _request.Headers;
            int count = headers.Count;
            string[][] result = new string[count][];
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                string key = headers.GetKey(i);
                result[n] = new string[] { key, headers.Get(i) };
                n++;
            }
            return result;
        }

        public override string GetServerVariable(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }
            switch (name)
            {
                case "REQUEST_METHOD":
                    return GetHttpVerbName();
                case "SERVER_PROTOCOL":
                    return GetHttpVersion();
                case "QUERY_STRING":
                    return GetQueryString();
                case "REMOTE_ADDR":
                    return GetRemoteAddress();
                case "REMOTE_PORT":
                    return GetRemotePort().ToString();
                case "LOCAL_ADDR":
                    return GetLocalAddress();
                case "SERVER_PORT":
                    return GetLocalPort().ToString();
                case "SERVER_PORT_SECURE":
                    return IsSecure() ? "1" : "0";
                case "HTTPS":
                    return IsSecure() ? "on" : "off";
                case "PATH_INFO":
                    return GetUriPath();
                case "SCRIPT_NAME":
                    return GetFilePath();
                case "APPL_PHYSICAL_PATH":
                    return _physicalDir;
                default:
                    string mapped = _request.Headers[name];
                    return mapped == null ? string.Empty : mapped;
            }
        }

        // ----- Entity body (useful virtual) -----

        public override int ReadEntityBody(byte[] buffer, int size)
        {
            return ReadEntityBody(buffer, 0, size);
        }

        public override int ReadEntityBody(byte[] buffer, int offset, int size)
        {
            if (buffer == null || size <= 0)
            {
                return 0;
            }
            Stream input = _request.InputStream;
            if (input == null)
            {
                return 0;
            }
            return input.Read(buffer, offset, size);
        }

        public override bool IsClientConnected()
        {
            return true;
        }

        // ----- Response sinks (abstract) -----

        public override void SendStatus(int statusCode, string statusDescription)
        {
            _response.StatusCode = statusCode;
            if (statusDescription != null)
            {
                _response.StatusDescription = statusDescription;
            }
        }

        public override void SendKnownResponseHeader(int index, string value)
        {
            string name = global::System.Web.HttpWorkerRequest.GetKnownResponseHeaderName(index);
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            SetResponseHeader(name, value);
        }

        public override void SendUnknownResponseHeader(string name, string value)
        {
            SetResponseHeader(name, value);
        }

        private void SetResponseHeader(string name, string value)
        {
            // Some headers are managed via dedicated properties on HttpListenerResponse.
            if (string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                long len;
                if (long.TryParse(value, out len))
                {
                    _response.ContentLength64 = len;
                }
                return;
            }
            if (string.Equals(name, "Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                _response.ContentType = value;
                return;
            }
            try
            {
                _response.Headers[name] = value;
            }
            catch (ArgumentException)
            {
                // Restricted header that HttpListener refuses to set directly; ignore at Tier 0.
            }
        }

        public override void SendResponseFromMemory(byte[] data, int length)
        {
            if (data == null || length <= 0)
            {
                return;
            }
            _headersSent = true;
            _outputStream.Write(data, 0, length);
        }

        public override void SendResponseFromFile(string filename, long offset, long length)
        {
            if (string.IsNullOrEmpty(filename) || length <= 0)
            {
                return;
            }
            _headersSent = true;
            using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (offset > 0)
                {
                    fs.Seek(offset, SeekOrigin.Begin);
                }
                long remaining = length;
                byte[] buffer = new byte[8192];
                while (remaining > 0)
                {
                    int toRead = remaining < buffer.Length ? (int)remaining : buffer.Length;
                    int read = fs.Read(buffer, 0, toRead);
                    if (read <= 0)
                    {
                        break;
                    }
                    _outputStream.Write(buffer, 0, read);
                    remaining -= read;
                }
            }
        }

        public override void SendResponseFromFile(IntPtr handle, long offset, long length)
        {
            if (length <= 0)
            {
                return;
            }
            _headersSent = true;
            using (Microsoft.Win32.SafeHandles.SafeFileHandle safeHandle =
                       new Microsoft.Win32.SafeHandles.SafeFileHandle(handle, false))
            using (FileStream fs = new FileStream(safeHandle, FileAccess.Read))
            {
                if (offset > 0)
                {
                    fs.Seek(offset, SeekOrigin.Begin);
                }
                long remaining = length;
                byte[] buffer = new byte[8192];
                while (remaining > 0)
                {
                    int toRead = remaining < buffer.Length ? (int)remaining : buffer.Length;
                    int read = fs.Read(buffer, 0, toRead);
                    if (read <= 0)
                    {
                        break;
                    }
                    _outputStream.Write(buffer, 0, read);
                    remaining -= read;
                }
            }
        }

        public override void FlushResponse(bool finalFlush)
        {
            _headersSent = true;
            _outputStream.Flush();
        }

        public override bool HeadersSent()
        {
            return _headersSent;
        }

        public override void EndOfRequest()
        {
            try
            {
                _outputStream.Flush();
            }
            catch (Exception)
            {
                // Connection may already be torn down; ignore at Tier 0.
            }
            finally
            {
                _response.Close();
            }
        }
    }
}