// Clean-room standalone managed HTTP host (Mono/XSP style) for System.Web.
// NO ASP.NET Core. Owns a System.Net.HttpListener and dispatches into HttpRuntime.ProcessRequest.
// This type is NEW (not part of the frozen public skeleton); its method bodies are real code.
using System;
using System.Net;
using System.Text;
using System.Threading;

namespace System.Web.Server
{
    /// <summary>
    /// Swappable abstraction for a standalone HTTP server front end.
    /// </summary>
    internal interface IStandaloneServer
    {
        void Start();
        void Stop();
    }

    internal sealed class HttpListenerServer : IStandaloneServer
    {
        private readonly string _physicalDir;
        private readonly string _virtualDir;
        private readonly int _port;
        private readonly string _prefix;
        private readonly HttpListener _listener;
        private Thread _loopThread;
        private volatile bool _running;

        public HttpListenerServer(string physicalDir, string virtualDir, int port)
        {
            if (string.IsNullOrEmpty(physicalDir))
            {
                throw new ArgumentNullException("physicalDir");
            }
            _physicalDir = physicalDir;
            _virtualDir = string.IsNullOrEmpty(virtualDir) ? "/" : virtualDir;
            _port = port;
            _prefix = "http://localhost:" + port + "/";
            _listener = new HttpListener();
            _listener.Prefixes.Add(_prefix);
        }

        public string Prefix
        {
            get { return _prefix; }
        }

        public void Start()
        {
            if (_running)
            {
                return;
            }
            _listener.Start();
            _running = true;
            _loopThread = new Thread(AcceptLoop);
            _loopThread.IsBackground = true;
            _loopThread.Name = "HttpListenerServer";
            _loopThread.Start();
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                HttpListenerContext context;
                try
                {
                    context = _listener.GetContext();
                }
                catch (HttpListenerException)
                {
                    // Listener stopped/disposed while blocked in GetContext.
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (InvalidOperationException)
                {
                    break;
                }
                Dispatch(context);
            }
        }

        private void Dispatch(HttpListenerContext context)
        {
            try
            {
                // NOTE: at Tier 0 the frozen base HttpWorkerRequest ctor itself throws
                // NotImplementedException, so even constructing the worker request must be
                // inside the guard. Construction stays here so the accept loop survives.
                ListenerWorkerRequest wr = new ListenerWorkerRequest(context, _physicalDir, _virtualDir);
                global::System.Web.HttpRuntime.ProcessRequest(wr);
            }
            catch (NotImplementedException)
            {
                // Tier 0: the runtime spine is not implemented yet. Emit a 500 and keep serving.
                WriteFailure(context, "500 Internal Server Error: runtime not implemented (Tier 0 stub)");
            }
            catch (Exception ex)
            {
                WriteFailure(context, "500 Internal Server Error: " + ex.GetType().Name);
            }
        }

        private static void WriteFailure(HttpListenerContext context, string message)
        {
            try
            {
                HttpListenerResponse response = context.Response;
                response.StatusCode = 500;
                response.StatusDescription = "Internal Server Error";
                response.ContentType = "text/plain; charset=utf-8";
                byte[] body = Encoding.UTF8.GetBytes(message);
                response.ContentLength64 = body.Length;
                response.OutputStream.Write(body, 0, body.Length);
            }
            catch (Exception)
            {
                // Best effort only.
            }
            finally
            {
                try
                {
                    context.Response.Close();
                }
                catch (Exception)
                {
                }
            }
        }

        public void Stop()
        {
            if (!_running)
            {
                return;
            }
            _running = false;
            try
            {
                _listener.Stop();
            }
            catch (Exception)
            {
            }
            try
            {
                _listener.Close();
            }
            catch (Exception)
            {
            }
            Thread t = _loopThread;
            if (t != null && t.IsAlive)
            {
                t.Join(2000);
            }
        }
    }
}
