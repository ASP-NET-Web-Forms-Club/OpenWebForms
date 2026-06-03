// clean-room System.Web.WebSockets implementation.
#nullable disable
#pragma warning disable

namespace System.Web.WebSockets
{
    public sealed class AspNetWebSocket : global::System.Net.WebSockets.WebSocket
    {
        // The underlying transport socket negotiated by the host during the HTTP upgrade.
        // When the host has not (yet) performed the upgrade this is null and all operations
        // surface an InvalidOperationException, matching the documented "must be connected"
        // behavior.
        private readonly global::System.Net.WebSockets.WebSocket _inner;
        private readonly global::System.String _subProtocol;
        private static global::System.Int32 _connectionCount;

        internal AspNetWebSocket() : this(null, null) { }

        internal AspNetWebSocket(global::System.Net.WebSockets.WebSocket inner, global::System.String subProtocol)
        {
            _inner = inner;
            _subProtocol = subProtocol;
            if (inner != null)
            {
                global::System.Threading.Interlocked.Increment(ref _connectionCount);
            }
        }

        internal static global::System.Int32 ActiveConnectionCount { get { return _connectionCount; } }

        private global::System.Net.WebSockets.WebSocket Require()
        {
            if (_inner == null)
            {
                throw new global::System.InvalidOperationException("The WebSocket connection has not been established by the host.");
            }
            return _inner;
        }

        public override void Abort()
        {
            if (_inner != null) { _inner.Abort(); }
        }
        public override global::System.Threading.Tasks.Task CloseAsync(global::System.Net.WebSockets.WebSocketCloseStatus closeStatus, global::System.String statusDescription, global::System.Threading.CancellationToken cancellationToken)
        {
            return Require().CloseAsync(closeStatus, statusDescription, cancellationToken);
        }
        public override global::System.Threading.Tasks.Task CloseOutputAsync(global::System.Net.WebSockets.WebSocketCloseStatus closeStatus, global::System.String statusDescription, global::System.Threading.CancellationToken cancellationToken)
        {
            return Require().CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
        }
        public override void Dispose()
        {
            if (_inner != null)
            {
                global::System.Threading.Interlocked.Decrement(ref _connectionCount);
                _inner.Dispose();
            }
        }
        public override global::System.Threading.Tasks.Task<global::System.Net.WebSockets.WebSocketReceiveResult> ReceiveAsync(global::System.ArraySegment<global::System.Byte> buffer, global::System.Threading.CancellationToken cancellationToken)
        {
            return Require().ReceiveAsync(buffer, cancellationToken);
        }
        public override global::System.Threading.Tasks.Task SendAsync(global::System.ArraySegment<global::System.Byte> buffer, global::System.Net.WebSockets.WebSocketMessageType messageType, global::System.Boolean endOfMessage, global::System.Threading.CancellationToken cancellationToken)
        {
            return Require().SendAsync(buffer, messageType, endOfMessage, cancellationToken);
        }
        public override global::System.Nullable<global::System.Net.WebSockets.WebSocketCloseStatus> CloseStatus
        {
            get { return _inner == null ? null : _inner.CloseStatus; }
        }
        public override global::System.String CloseStatusDescription
        {
            get { return _inner == null ? null : _inner.CloseStatusDescription; }
        }
        public override global::System.Net.WebSockets.WebSocketState State
        {
            get { return _inner == null ? global::System.Net.WebSockets.WebSocketState.None : _inner.State; }
        }
        public override global::System.String SubProtocol
        {
            get { return _subProtocol != null ? _subProtocol : (_inner == null ? null : _inner.SubProtocol); }
        }
    }
    public sealed class AspNetWebSocketOptions
    {
        public AspNetWebSocketOptions() { }

        // When true the negotiated connection must originate from the same origin as the
        // hosting application; the upgrade handshake rejects mismatched Origin headers.
        public global::System.Boolean RequireSameOrigin { get; set; }

        // The application-level sub-protocol to advertise during the handshake.
        public global::System.String SubProtocol { get; set; }
    }
    public abstract class AspNetWebSocketContext : global::System.Net.WebSockets.WebSocketContext
    {
        protected AspNetWebSocketContext() { }

        // The following virtuals expose the originating HTTP request's ambient state. The
        // base implementation returns inert defaults; a concrete host-provided context
        // overrides them with live request data once the upgrade pipeline is wired.
        public virtual global::System.String AnonymousID { get { return null; } }
        public virtual global::System.Web.HttpApplicationStateBase Application { get { return null; } }
        public virtual global::System.String ApplicationPath { get { return null; } }
        public virtual global::System.Web.Caching.Cache Cache { get { return null; } }
        public virtual global::System.Web.HttpClientCertificate ClientCertificate { get { return null; } }
        public static global::System.Int32 ConnectionCount { get { return global::System.Web.WebSockets.AspNetWebSocket.ActiveConnectionCount; } }
        public override global::System.Net.CookieCollection CookieCollection { get { return null; } }
        public virtual global::System.Web.HttpCookieCollection Cookies { get { return null; } }
        public virtual global::System.String FilePath { get { return null; } }
        public override global::System.Collections.Specialized.NameValueCollection Headers { get { return null; } }
        public override global::System.Boolean IsAuthenticated { get { return false; } }
        public virtual global::System.Boolean IsClientConnected { get { return true; } }
        public virtual global::System.Boolean IsDebuggingEnabled { get { return false; } }
        public override global::System.Boolean IsLocal { get { return false; } }
        public override global::System.Boolean IsSecureConnection { get { return false; } }
        public virtual global::System.Collections.IDictionary Items { get { return null; } }
        public virtual global::System.Security.Principal.WindowsIdentity LogonUserIdentity { get { return null; } }
        public override global::System.String Origin { get { return null; } }
        public virtual global::System.String Path { get { return null; } }
        public virtual global::System.String PathInfo { get { return null; } }
        public virtual global::System.Web.Profile.ProfileBase Profile { get { return null; } }
        public virtual global::System.Collections.Specialized.NameValueCollection QueryString { get { return null; } }
        public virtual global::System.String RawUrl { get { return null; } }
        public override global::System.Uri RequestUri { get { return null; } }
        public override global::System.String SecWebSocketKey { get { return null; } }
        public override global::System.Collections.Generic.IEnumerable<global::System.String> SecWebSocketProtocols
        {
            get { return new global::System.String[0]; }
        }
        public override global::System.String SecWebSocketVersion { get { return null; } }
        public virtual global::System.Web.HttpServerUtilityBase Server { get { return null; } }
        public virtual global::System.Collections.Specialized.NameValueCollection ServerVariables { get { return null; } }
        public virtual global::System.DateTime Timestamp { get { return global::System.DateTime.MinValue; } }
        public virtual global::System.Web.UnvalidatedRequestValuesBase Unvalidated { get { return null; } }
        public virtual global::System.Uri UrlReferrer { get { return null; } }
        public override global::System.Security.Principal.IPrincipal User { get { return null; } }
        public virtual global::System.String UserAgent { get { return null; } }
        public virtual global::System.String UserHostAddress { get { return null; } }
        public virtual global::System.String UserHostName { get { return null; } }
        public virtual global::System.String[] UserLanguages { get { return null; } }
        // The negotiated socket; populated by a derived host context after the HTTP upgrade.
        public override global::System.Net.WebSockets.WebSocket WebSocket { get { return null; } }
    }
}
