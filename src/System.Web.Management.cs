// Clean-room System.Web.Management (health monitoring / web events).
// Behavior implemented from documented semantics; no MS source/IL consulted.
#nullable disable
#pragma warning disable

namespace System.Web.Management
{
    // ---- Event formatter -------------------------------------------------
    public class WebEventFormatter
    {
        private readonly global::System.Text.StringBuilder _sb = new global::System.Text.StringBuilder();
        private global::System.Int32 _indent;
        private global::System.Int32 _tabSize = 4;
        internal WebEventFormatter() { }
        private void AppendIndent()
        {
            global::System.Int32 spaces = _indent * _tabSize;
            for (global::System.Int32 i = 0; i < spaces; i++) { _sb.Append(' '); }
        }
        public void AppendLine(global::System.String s)
        {
            AppendIndent();
            _sb.Append(s == null ? global::System.String.Empty : s);
            _sb.Append(global::System.Environment.NewLine);
        }
        public override global::System.String ToString() { return _sb.ToString(); }
        public global::System.Int32 IndentationLevel
        {
            get { return _indent; }
            set { _indent = value < 0 ? 0 : value; }
        }
        public global::System.Int32 TabSize
        {
            get { return _tabSize; }
            set { _tabSize = value < 0 ? 0 : value; }
        }
    }

    // ---- Informational value objects ------------------------------------
    public sealed class WebApplicationInformation
    {
        private readonly global::System.String _appDomain;
        private readonly global::System.String _trustLevel;
        private readonly global::System.String _appVirtualPath;
        private readonly global::System.String _appPath;
        private readonly global::System.String _machineName;
        internal WebApplicationInformation()
        {
            _appDomain = global::System.AppDomain.CurrentDomain.FriendlyName;
            _trustLevel = "Full";
            global::System.String ppath = null;
            try { ppath = global::System.AppDomain.CurrentDomain.BaseDirectory; } catch { }
            if (global::System.String.IsNullOrEmpty(ppath)) { ppath = global::System.IO.Directory.GetCurrentDirectory(); }
            _appVirtualPath = "/";
            _appPath = ppath;
            _machineName = global::System.Environment.MachineName;
        }
        public void FormatToString(global::System.Web.Management.WebEventFormatter formatter)
        {
            if (formatter == null) { return; }
            formatter.AppendLine("Application Information:");
            formatter.IndentationLevel += 1;
            formatter.AppendLine("Application domain: " + _appDomain);
            formatter.AppendLine("Trust level: " + _trustLevel);
            formatter.AppendLine("Application Virtual Path: " + _appVirtualPath);
            formatter.AppendLine("Application Path: " + _appPath);
            formatter.AppendLine("Machine name: " + _machineName);
            formatter.IndentationLevel -= 1;
        }
        public override global::System.String ToString()
        {
            global::System.Web.Management.WebEventFormatter f = new global::System.Web.Management.WebEventFormatter();
            FormatToString(f);
            return f.ToString();
        }
        public global::System.String ApplicationDomain { get { return _appDomain; } }
        public global::System.String TrustLevel { get { return _trustLevel; } }
        public global::System.String ApplicationVirtualPath { get { return _appVirtualPath; } }
        public global::System.String ApplicationPath { get { return _appPath; } }
        public global::System.String MachineName { get { return _machineName; } }
    }

    public sealed class WebProcessInformation
    {
        private readonly global::System.Int32 _processId;
        private readonly global::System.String _processName;
        private readonly global::System.String _accountName;
        internal WebProcessInformation()
        {
            global::System.Int32 pid = 0;
            global::System.String pname = global::System.String.Empty;
            try
            {
                global::System.Diagnostics.Process p = global::System.Diagnostics.Process.GetCurrentProcess();
                pid = p.Id;
                pname = p.ProcessName;
            }
            catch { }
            _processId = pid;
            _processName = pname;
            global::System.String acct = global::System.String.Empty;
            try { acct = global::System.Environment.UserDomainName + "\\" + global::System.Environment.UserName; }
            catch { }
            _accountName = acct;
        }
        public void FormatToString(global::System.Web.Management.WebEventFormatter formatter)
        {
            if (formatter == null) { return; }
            formatter.AppendLine("Process information:");
            formatter.IndentationLevel += 1;
            formatter.AppendLine("Process ID: " + _processId.ToString(global::System.Globalization.CultureInfo.InstalledUICulture));
            formatter.AppendLine("Process name: " + _processName);
            formatter.AppendLine("Account name: " + _accountName);
            formatter.IndentationLevel -= 1;
        }
        public global::System.Int32 ProcessID { get { return _processId; } }
        public global::System.String ProcessName { get { return _processName; } }
        public global::System.String AccountName { get { return _accountName; } }
    }

    public sealed class WebRequestInformation
    {
        private readonly global::System.String _requestUrl;
        private readonly global::System.String _requestPath;
        private readonly global::System.Security.Principal.IPrincipal _principal;
        private readonly global::System.String _userHostAddress;
        private readonly global::System.String _threadAccountName;
        internal WebRequestInformation()
        {
            _requestUrl = global::System.String.Empty;
            _requestPath = global::System.String.Empty;
            _userHostAddress = global::System.String.Empty;
            global::System.Security.Principal.IPrincipal principal = null;
            try { principal = global::System.Threading.Thread.CurrentPrincipal; } catch { }
            _principal = principal;
            global::System.String acct = global::System.String.Empty;
            try { acct = global::System.Environment.UserDomainName + "\\" + global::System.Environment.UserName; } catch { }
            _threadAccountName = acct;
        }
        public void FormatToString(global::System.Web.Management.WebEventFormatter formatter)
        {
            if (formatter == null) { return; }
            formatter.AppendLine("Request information:");
            formatter.IndentationLevel += 1;
            formatter.AppendLine("Request URL: " + _requestUrl);
            formatter.AppendLine("Request path: " + _requestPath);
            formatter.AppendLine("User host address: " + _userHostAddress);
            global::System.String user = global::System.String.Empty;
            global::System.Boolean authed = false;
            if (_principal != null && _principal.Identity != null)
            {
                user = _principal.Identity.Name;
                authed = _principal.Identity.IsAuthenticated;
            }
            formatter.AppendLine("User: " + (user == null ? global::System.String.Empty : user));
            formatter.AppendLine("Is authenticated: " + authed.ToString());
            formatter.AppendLine("Authentication Type: ");
            formatter.AppendLine("Thread account name: " + _threadAccountName);
            formatter.IndentationLevel -= 1;
        }
        public global::System.String RequestUrl { get { return _requestUrl; } }
        public global::System.String RequestPath { get { return _requestPath; } }
        public global::System.Security.Principal.IPrincipal Principal { get { return _principal; } }
        public global::System.String UserHostAddress { get { return _userHostAddress; } }
        public global::System.String ThreadAccountName { get { return _threadAccountName; } }
    }

    public sealed class WebThreadInformation
    {
        private readonly global::System.Int32 _threadId;
        private readonly global::System.String _threadAccountName;
        private readonly global::System.String _stackTrace;
        private readonly global::System.Boolean _isImpersonating;
        internal WebThreadInformation() : this(null) { }
        internal WebThreadInformation(global::System.Exception e)
        {
            _threadId = global::System.Environment.CurrentManagedThreadId;
            global::System.String acct = global::System.String.Empty;
            try { acct = global::System.Environment.UserDomainName + "\\" + global::System.Environment.UserName; } catch { }
            _threadAccountName = acct;
            global::System.String trace = global::System.String.Empty;
            if (e != null && e.StackTrace != null) { trace = e.StackTrace; }
            else
            {
                try { trace = global::System.Environment.StackTrace; } catch { }
            }
            _stackTrace = trace;
            _isImpersonating = false;
        }
        public void FormatToString(global::System.Web.Management.WebEventFormatter formatter)
        {
            if (formatter == null) { return; }
            formatter.AppendLine("Thread information:");
            formatter.IndentationLevel += 1;
            formatter.AppendLine("Thread ID: " + _threadId.ToString(global::System.Globalization.CultureInfo.InstalledUICulture));
            formatter.AppendLine("Thread account name: " + _threadAccountName);
            formatter.AppendLine("Is impersonating: " + _isImpersonating.ToString());
            formatter.AppendLine("Stack trace: " + _stackTrace);
            formatter.IndentationLevel -= 1;
        }
        public global::System.Int32 ThreadID { get { return _threadId; } }
        public global::System.String ThreadAccountName { get { return _threadAccountName; } }
        public global::System.String StackTrace { get { return _stackTrace; } }
        public global::System.Boolean IsImpersonating { get { return _isImpersonating; } }
    }

    public class WebProcessStatistics
    {
        private readonly global::System.DateTime _startTime;
        private readonly global::System.Int32 _threadCount;
        private readonly global::System.Int64 _workingSet;
        private readonly global::System.Int64 _peakWorkingSet;
        private readonly global::System.Int64 _managedHeapSize;
        public WebProcessStatistics()
        {
            global::System.DateTime start = global::System.DateTime.Now;
            global::System.Int32 threads = 0;
            global::System.Int64 ws = 0;
            global::System.Int64 peak = 0;
            try
            {
                global::System.Diagnostics.Process p = global::System.Diagnostics.Process.GetCurrentProcess();
                try { start = p.StartTime; } catch { }
                try { threads = p.Threads.Count; } catch { }
                ws = p.WorkingSet64;
                peak = p.PeakWorkingSet64;
            }
            catch { }
            _startTime = start;
            _threadCount = threads;
            _workingSet = ws;
            _peakWorkingSet = peak;
            _managedHeapSize = global::System.GC.GetTotalMemory(false);
        }
        public virtual void FormatToString(global::System.Web.Management.WebEventFormatter formatter)
        {
            if (formatter == null) { return; }
            global::System.Globalization.CultureInfo ci = global::System.Globalization.CultureInfo.InstalledUICulture;
            formatter.AppendLine("Process statistics:");
            formatter.IndentationLevel += 1;
            formatter.AppendLine("Process start time: " + _startTime.ToString(ci));
            formatter.AppendLine("Thread count: " + _threadCount.ToString(ci));
            formatter.AppendLine("Working set: " + _workingSet.ToString(ci));
            formatter.AppendLine("Peak working set: " + _peakWorkingSet.ToString(ci));
            formatter.AppendLine("Managed heap size: " + _managedHeapSize.ToString(ci));
            formatter.AppendLine("Application domain count: " + AppDomainCount.ToString(ci));
            formatter.AppendLine("Requests executing: " + RequestsExecuting.ToString(ci));
            formatter.AppendLine("Requests queued: " + RequestsQueued.ToString(ci));
            formatter.AppendLine("Requests rejected: " + RequestsRejected.ToString(ci));
            formatter.IndentationLevel -= 1;
        }
        public global::System.DateTime ProcessStartTime { get { return _startTime; } }
        public global::System.Int32 ThreadCount { get { return _threadCount; } }
        public global::System.Int64 WorkingSet { get { return _workingSet; } }
        public global::System.Int64 PeakWorkingSet { get { return _peakWorkingSet; } }
        public global::System.Int64 ManagedHeapSize { get { return _managedHeapSize; } }
        public global::System.Int32 AppDomainCount { get { return 1; } }
        public global::System.Int32 RequestsExecuting { get { return 0; } }
        public global::System.Int32 RequestsQueued { get { return 0; } }
        public global::System.Int32 RequestsRejected { get { return 0; } }
    }

    public sealed class RuleFiringRecord
    {
        private readonly global::System.DateTime _lastFired;
        private readonly global::System.Int32 _timesRaised;
        internal RuleFiringRecord() { _lastFired = global::System.DateTime.UtcNow; _timesRaised = 0; }
        internal RuleFiringRecord(global::System.DateTime lastFired, global::System.Int32 timesRaised)
        {
            _lastFired = lastFired;
            _timesRaised = timesRaised;
        }
        public global::System.DateTime LastFired { get { return _lastFired; } }
        public global::System.Int32 TimesRaised { get { return _timesRaised; } }
    }

    public interface IWebEventCustomEvaluator
    {
        global::System.Boolean CanFire(global::System.Web.Management.WebBaseEvent raisedEvent, global::System.Web.Management.RuleFiringRecord record);
    }

    // ---- WebEventCodes ---------------------------------------------------
    public sealed class WebEventCodes
    {
        internal WebEventCodes() { }
        public const global::System.Int32 InvalidEventCode = -1;
        public const global::System.Int32 UndefinedEventCode = 0;
        public const global::System.Int32 UndefinedEventDetailCode = 0;
        public const global::System.Int32 ApplicationCodeBase = 1000;
        public const global::System.Int32 ApplicationStart = 1001;
        public const global::System.Int32 ApplicationShutdown = 1002;
        public const global::System.Int32 ApplicationCompilationStart = 1003;
        public const global::System.Int32 ApplicationCompilationEnd = 1004;
        public const global::System.Int32 ApplicationHeartbeat = 1005;
        public const global::System.Int32 RequestCodeBase = 2000;
        public const global::System.Int32 RequestTransactionComplete = 2001;
        public const global::System.Int32 RequestTransactionAbort = 2002;
        public const global::System.Int32 ErrorCodeBase = 3000;
        public const global::System.Int32 RuntimeErrorRequestAbort = 3001;
        public const global::System.Int32 RuntimeErrorViewStateFailure = 3002;
        public const global::System.Int32 RuntimeErrorValidationFailure = 3003;
        public const global::System.Int32 RuntimeErrorPostTooLarge = 3004;
        public const global::System.Int32 RuntimeErrorUnhandledException = 3005;
        public const global::System.Int32 WebErrorParserError = 3006;
        public const global::System.Int32 WebErrorCompilationError = 3007;
        public const global::System.Int32 WebErrorConfigurationError = 3008;
        public const global::System.Int32 WebErrorOtherError = 3009;
        public const global::System.Int32 WebErrorPropertyDeserializationError = 3010;
        public const global::System.Int32 WebErrorObjectStateFormatterDeserializationError = 3011;
        public const global::System.Int32 RuntimeErrorWebResourceFailure = 3012;
        public const global::System.Int32 AuditCodeBase = 4000;
        public const global::System.Int32 AuditFormsAuthenticationSuccess = 4001;
        public const global::System.Int32 AuditMembershipAuthenticationSuccess = 4002;
        public const global::System.Int32 AuditUrlAuthorizationSuccess = 4003;
        public const global::System.Int32 AuditFileAuthorizationSuccess = 4004;
        public const global::System.Int32 AuditFormsAuthenticationFailure = 4005;
        public const global::System.Int32 AuditMembershipAuthenticationFailure = 4006;
        public const global::System.Int32 AuditUrlAuthorizationFailure = 4007;
        public const global::System.Int32 AuditFileAuthorizationFailure = 4008;
        public const global::System.Int32 AuditInvalidViewStateFailure = 4009;
        public const global::System.Int32 AuditUnhandledSecurityException = 4010;
        public const global::System.Int32 AuditUnhandledAccessException = 4011;
        public const global::System.Int32 MiscCodeBase = 6000;
        public const global::System.Int32 WebEventProviderInformation = 6001;
        public const global::System.Int32 ApplicationDetailCodeBase = 50000;
        public const global::System.Int32 ApplicationShutdownUnknown = 50001;
        public const global::System.Int32 ApplicationShutdownHostingEnvironment = 50002;
        public const global::System.Int32 ApplicationShutdownChangeInGlobalAsax = 50003;
        public const global::System.Int32 ApplicationShutdownConfigurationChange = 50004;
        public const global::System.Int32 ApplicationShutdownUnloadAppDomainCalled = 50005;
        public const global::System.Int32 ApplicationShutdownChangeInSecurityPolicyFile = 50006;
        public const global::System.Int32 ApplicationShutdownBinDirChangeOrDirectoryRename = 50007;
        public const global::System.Int32 ApplicationShutdownBrowsersDirChangeOrDirectoryRename = 50008;
        public const global::System.Int32 ApplicationShutdownCodeDirChangeOrDirectoryRename = 50009;
        public const global::System.Int32 ApplicationShutdownResourcesDirChangeOrDirectoryRename = 50010;
        public const global::System.Int32 ApplicationShutdownIdleTimeout = 50011;
        public const global::System.Int32 ApplicationShutdownPhysicalApplicationPathChanged = 50012;
        public const global::System.Int32 ApplicationShutdownHttpRuntimeClose = 50013;
        public const global::System.Int32 ApplicationShutdownInitializationError = 50014;
        public const global::System.Int32 ApplicationShutdownMaxRecompilationsReached = 50015;
        public const global::System.Int32 StateServerConnectionError = 50016;
        public const global::System.Int32 ApplicationShutdownBuildManagerChange = 50017;
        public const global::System.Int32 AuditDetailCodeBase = 50200;
        public const global::System.Int32 InvalidTicketFailure = 50201;
        public const global::System.Int32 ExpiredTicketFailure = 50202;
        public const global::System.Int32 InvalidViewStateMac = 50203;
        public const global::System.Int32 InvalidViewState = 50204;
        public const global::System.Int32 WebEventDetailCodeBase = 50300;
        public const global::System.Int32 SqlProviderEventsDropped = 50301;
        public const global::System.Int32 WebExtendedBase = 100000;
    }

    // ---- WebBaseEvent and hierarchy -------------------------------------
    public class WebBaseEvent
    {
        private static readonly global::System.Object s_lock = new global::System.Object();
        private static global::System.Web.Management.WebApplicationInformation s_appInfo;
        private static long s_sequence;

        private readonly global::System.String _message;
        private readonly global::System.Object _eventSource;
        private readonly global::System.Int32 _eventCode;
        private readonly global::System.Int32 _eventDetailCode;
        private readonly global::System.DateTime _eventTimeUtc;
        private readonly long _eventSequence;
        private readonly global::System.Guid _eventId;

        protected internal WebBaseEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode)
            : this(message, eventSource, eventCode, 0) { }
        protected internal WebBaseEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode, global::System.Int32 eventDetailCode)
        {
            _message = message;
            _eventSource = eventSource;
            _eventCode = eventCode;
            _eventDetailCode = eventDetailCode;
            _eventTimeUtc = global::System.DateTime.UtcNow;
            _eventSequence = global::System.Threading.Interlocked.Increment(ref s_sequence);
            _eventId = global::System.Guid.NewGuid();
        }

        public override global::System.String ToString() { return ToString(true, true); }
        public virtual global::System.String ToString(global::System.Boolean includeAppInfo, global::System.Boolean includeCustomEventDetails)
        {
            global::System.Web.Management.WebEventFormatter formatter = new global::System.Web.Management.WebEventFormatter();
            global::System.Globalization.CultureInfo ci = global::System.Globalization.CultureInfo.InstalledUICulture;
            formatter.AppendLine("Event code: " + _eventCode.ToString(ci));
            formatter.AppendLine("Event message: " + (_message == null ? global::System.String.Empty : _message));
            formatter.AppendLine("Event time: " + EventTime.ToString(ci));
            formatter.AppendLine("Event time (UTC): " + _eventTimeUtc.ToString(ci));
            formatter.AppendLine("Event ID: " + _eventId.ToString("N", ci));
            formatter.AppendLine("Event sequence: " + _eventSequence.ToString(ci));
            formatter.AppendLine("Event occurrence: " + EventOccurrence.ToString(ci));
            formatter.AppendLine("Event detail code: " + _eventDetailCode.ToString(ci));
            if (includeAppInfo)
            {
                formatter.AppendLine(global::System.String.Empty);
                ApplicationInformation.FormatToString(formatter);
            }
            if (includeCustomEventDetails)
            {
                formatter.AppendLine(global::System.String.Empty);
                FormatCustomEventDetails(formatter);
            }
            return formatter.ToString();
        }

        public virtual void FormatCustomEventDetails(global::System.Web.Management.WebEventFormatter formatter) { }
        protected internal virtual void IncrementPerfCounters() { }

        public virtual void Raise() { global::System.Web.Management.WebBaseEvent.Raise(this); }
        public static void Raise(global::System.Web.Management.WebBaseEvent eventRaised)
        {
            if (eventRaised == null) { return; }
            try { eventRaised.IncrementPerfCounters(); } catch { }
            // Clean-room routing: the full .NET Framework reads <healthMonitoring>
            // rules and dispatches to matched providers. Those config members are
            // not yet implemented in this build, so we route to the process-wide
            // provider set registered via the internal manager.
            global::System.Web.Management.WebEventManager.RaiseToProviders(eventRaised);
        }

        public global::System.DateTime EventTime { get { return _eventTimeUtc.ToLocalTime(); } }
        public global::System.DateTime EventTimeUtc { get { return _eventTimeUtc; } }
        public global::System.String Message { get { return _message; } }
        public global::System.Object EventSource { get { return _eventSource; } }
        public global::System.Int64 EventSequence { get { return _eventSequence; } }
        public global::System.Int64 EventOccurrence { get { return _eventSequence; } }
        public global::System.Int32 EventCode { get { return _eventCode; } }
        public global::System.Int32 EventDetailCode { get { return _eventDetailCode; } }
        public global::System.Guid EventID { get { return _eventId; } }
        public static global::System.Web.Management.WebApplicationInformation ApplicationInformation
        {
            get
            {
                if (s_appInfo == null)
                {
                    lock (s_lock)
                    {
                        if (s_appInfo == null) { s_appInfo = new global::System.Web.Management.WebApplicationInformation(); }
                    }
                }
                return s_appInfo;
            }
        }
    }

    public class WebManagementEvent : global::System.Web.Management.WebBaseEvent
    {
        private static global::System.Web.Management.WebProcessInformation s_processInfo;
        private static readonly global::System.Object s_piLock = new global::System.Object();
        protected internal WebManagementEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode)
            : base(message, eventSource, eventCode) { }
        protected internal WebManagementEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode, global::System.Int32 eventDetailCode)
            : base(message, eventSource, eventCode, eventDetailCode) { }
        public global::System.Web.Management.WebProcessInformation ProcessInformation
        {
            get
            {
                if (s_processInfo == null)
                {
                    lock (s_piLock)
                    {
                        if (s_processInfo == null) { s_processInfo = new global::System.Web.Management.WebProcessInformation(); }
                    }
                }
                return s_processInfo;
            }
        }
        public override void FormatCustomEventDetails(global::System.Web.Management.WebEventFormatter formatter)
        {
            if (formatter != null) { ProcessInformation.FormatToString(formatter); }
        }
    }

    public class WebHeartbeatEvent : global::System.Web.Management.WebManagementEvent
    {
        private readonly global::System.Web.Management.WebProcessStatistics _stats;
        protected internal WebHeartbeatEvent(global::System.String message, global::System.Int32 eventCode)
            : base(message, null, eventCode)
        {
            _stats = new global::System.Web.Management.WebProcessStatistics();
        }
        public global::System.Web.Management.WebProcessStatistics ProcessStatistics { get { return _stats; } }
        public override void FormatCustomEventDetails(global::System.Web.Management.WebEventFormatter formatter)
        {
            if (formatter != null) { _stats.FormatToString(formatter); }
        }
    }

    public class WebApplicationLifetimeEvent : global::System.Web.Management.WebManagementEvent
    {
        protected internal WebApplicationLifetimeEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode)
            : base(message, eventSource, eventCode) { }
        protected internal WebApplicationLifetimeEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode, global::System.Int32 eventDetailCode)
            : base(message, eventSource, eventCode, eventDetailCode) { }
        protected internal override void IncrementPerfCounters() { }
    }

    public class WebRequestEvent : global::System.Web.Management.WebManagementEvent
    {
        private readonly global::System.Web.Management.WebRequestInformation _requestInfo;
        protected internal WebRequestEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode)
            : base(message, eventSource, eventCode)
        {
            _requestInfo = new global::System.Web.Management.WebRequestInformation();
        }
        protected internal WebRequestEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode, global::System.Int32 eventDetailCode)
            : base(message, eventSource, eventCode, eventDetailCode)
        {
            _requestInfo = new global::System.Web.Management.WebRequestInformation();
        }
        protected internal override void IncrementPerfCounters() { }
        public global::System.Web.Management.WebRequestInformation RequestInformation { get { return _requestInfo; } }
        public override void FormatCustomEventDetails(global::System.Web.Management.WebEventFormatter formatter)
        {
            base.FormatCustomEventDetails(formatter);
            if (formatter != null) { _requestInfo.FormatToString(formatter); }
        }
    }

    public class WebBaseErrorEvent : global::System.Web.Management.WebManagementEvent
    {
        private readonly global::System.Exception _exception;
        protected internal WebBaseErrorEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode, global::System.Exception e)
            : base(message, eventSource, eventCode)
        {
            _exception = e;
        }
        protected internal WebBaseErrorEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode, global::System.Int32 eventDetailCode, global::System.Exception e)
            : base(message, eventSource, eventCode, eventDetailCode)
        {
            _exception = e;
        }
        protected internal override void IncrementPerfCounters() { }
        public global::System.Exception ErrorException { get { return _exception; } }
        public override void FormatCustomEventDetails(global::System.Web.Management.WebEventFormatter formatter)
        {
            base.FormatCustomEventDetails(formatter);
            if (formatter != null && _exception != null)
            {
                formatter.AppendLine("Exception information:");
                formatter.IndentationLevel += 1;
                formatter.AppendLine("Exception type: " + _exception.GetType().FullName);
                formatter.AppendLine("Exception message: " + _exception.Message);
                formatter.IndentationLevel -= 1;
            }
        }
    }

    public class WebErrorEvent : global::System.Web.Management.WebBaseErrorEvent
    {
        private readonly global::System.Web.Management.WebRequestInformation _requestInfo;
        private readonly global::System.Web.Management.WebThreadInformation _threadInfo;
        protected internal WebErrorEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode, global::System.Exception exception)
            : base(message, eventSource, eventCode, exception)
        {
            _requestInfo = new global::System.Web.Management.WebRequestInformation();
            _threadInfo = new global::System.Web.Management.WebThreadInformation(exception);
        }
        protected internal WebErrorEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode, global::System.Int32 eventDetailCode, global::System.Exception exception)
            : base(message, eventSource, eventCode, eventDetailCode, exception)
        {
            _requestInfo = new global::System.Web.Management.WebRequestInformation();
            _threadInfo = new global::System.Web.Management.WebThreadInformation(exception);
        }
        protected internal override void IncrementPerfCounters() { }
        public global::System.Web.Management.WebRequestInformation RequestInformation { get { return _requestInfo; } }
        public global::System.Web.Management.WebThreadInformation ThreadInformation { get { return _threadInfo; } }
        public override void FormatCustomEventDetails(global::System.Web.Management.WebEventFormatter formatter)
        {
            base.FormatCustomEventDetails(formatter);
            if (formatter != null) { _requestInfo.FormatToString(formatter); _threadInfo.FormatToString(formatter); }
        }
    }

    public class WebRequestErrorEvent : global::System.Web.Management.WebBaseErrorEvent
    {
        private readonly global::System.Web.Management.WebRequestInformation _requestInfo;
        private readonly global::System.Web.Management.WebThreadInformation _threadInfo;
        protected internal WebRequestErrorEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode, global::System.Exception exception)
            : base(message, eventSource, eventCode, exception)
        {
            _requestInfo = new global::System.Web.Management.WebRequestInformation();
            _threadInfo = new global::System.Web.Management.WebThreadInformation(exception);
        }
        protected internal WebRequestErrorEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode, global::System.Int32 eventDetailCode, global::System.Exception exception)
            : base(message, eventSource, eventCode, eventDetailCode, exception)
        {
            _requestInfo = new global::System.Web.Management.WebRequestInformation();
            _threadInfo = new global::System.Web.Management.WebThreadInformation(exception);
        }
        protected internal override void IncrementPerfCounters() { }
        public global::System.Web.Management.WebRequestInformation RequestInformation { get { return _requestInfo; } }
        public global::System.Web.Management.WebThreadInformation ThreadInformation { get { return _threadInfo; } }
        public override void FormatCustomEventDetails(global::System.Web.Management.WebEventFormatter formatter)
        {
            base.FormatCustomEventDetails(formatter);
            if (formatter != null) { _requestInfo.FormatToString(formatter); _threadInfo.FormatToString(formatter); }
        }
    }

    public class WebAuditEvent : global::System.Web.Management.WebManagementEvent
    {
        private readonly global::System.Web.Management.WebRequestInformation _requestInfo;
        protected internal WebAuditEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode)
            : base(message, eventSource, eventCode)
        {
            _requestInfo = new global::System.Web.Management.WebRequestInformation();
        }
        protected internal WebAuditEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode, global::System.Int32 eventDetailCode)
            : base(message, eventSource, eventCode, eventDetailCode)
        {
            _requestInfo = new global::System.Web.Management.WebRequestInformation();
        }
        public global::System.Web.Management.WebRequestInformation RequestInformation { get { return _requestInfo; } }
        public override void FormatCustomEventDetails(global::System.Web.Management.WebEventFormatter formatter)
        {
            base.FormatCustomEventDetails(formatter);
            if (formatter != null) { _requestInfo.FormatToString(formatter); }
        }
    }

    public class WebFailureAuditEvent : global::System.Web.Management.WebAuditEvent
    {
        protected internal WebFailureAuditEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode)
            : base(message, eventSource, eventCode) { }
        protected internal WebFailureAuditEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode, global::System.Int32 eventDetailCode)
            : base(message, eventSource, eventCode, eventDetailCode) { }
        protected internal override void IncrementPerfCounters() { }
    }

    public class WebAuthenticationFailureAuditEvent : global::System.Web.Management.WebFailureAuditEvent
    {
        private readonly global::System.String _nameToAuthenticate;
        protected internal WebAuthenticationFailureAuditEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode, global::System.String nameToAuthenticate)
            : base(message, eventSource, eventCode)
        {
            _nameToAuthenticate = nameToAuthenticate;
        }
        protected internal WebAuthenticationFailureAuditEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode, global::System.Int32 eventDetailCode, global::System.String nameToAuthenticate)
            : base(message, eventSource, eventCode, eventDetailCode)
        {
            _nameToAuthenticate = nameToAuthenticate;
        }
        public global::System.String NameToAuthenticate { get { return _nameToAuthenticate; } }
        public override void FormatCustomEventDetails(global::System.Web.Management.WebEventFormatter formatter)
        {
            base.FormatCustomEventDetails(formatter);
            if (formatter != null) { formatter.AppendLine("Name to authenticate: " + (_nameToAuthenticate == null ? global::System.String.Empty : _nameToAuthenticate)); }
        }
    }

    public class WebViewStateFailureAuditEvent : global::System.Web.Management.WebFailureAuditEvent
    {
        private readonly global::System.Web.UI.ViewStateException _viewStateException;
        protected internal WebViewStateFailureAuditEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode, global::System.Web.UI.ViewStateException viewStateException)
            : base(message, eventSource, eventCode)
        {
            _viewStateException = viewStateException;
        }
        protected internal WebViewStateFailureAuditEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode, global::System.Int32 eventDetailCode, global::System.Web.UI.ViewStateException viewStateException)
            : base(message, eventSource, eventCode, eventDetailCode)
        {
            _viewStateException = viewStateException;
        }
        public global::System.Web.UI.ViewStateException ViewStateException { get { return _viewStateException; } }
        public override void FormatCustomEventDetails(global::System.Web.Management.WebEventFormatter formatter)
        {
            base.FormatCustomEventDetails(formatter);
            if (formatter != null && _viewStateException != null)
            {
                formatter.AppendLine("View state exception: " + _viewStateException.Message);
            }
        }
    }

    public class WebSuccessAuditEvent : global::System.Web.Management.WebAuditEvent
    {
        protected internal WebSuccessAuditEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode)
            : base(message, eventSource, eventCode) { }
        protected internal WebSuccessAuditEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode, global::System.Int32 eventDetailCode)
            : base(message, eventSource, eventCode, eventDetailCode) { }
        protected internal override void IncrementPerfCounters() { }
    }

    public class WebAuthenticationSuccessAuditEvent : global::System.Web.Management.WebSuccessAuditEvent
    {
        private readonly global::System.String _nameToAuthenticate;
        protected internal WebAuthenticationSuccessAuditEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode, global::System.String nameToAuthenticate)
            : base(message, eventSource, eventCode)
        {
            _nameToAuthenticate = nameToAuthenticate;
        }
        protected internal WebAuthenticationSuccessAuditEvent(global::System.String message, global::System.Object eventSource, global::System.Int32 eventCode, global::System.Int32 eventDetailCode, global::System.String nameToAuthenticate)
            : base(message, eventSource, eventCode, eventDetailCode)
        {
            _nameToAuthenticate = nameToAuthenticate;
        }
        public global::System.String NameToAuthenticate { get { return _nameToAuthenticate; } }
        public override void FormatCustomEventDetails(global::System.Web.Management.WebEventFormatter formatter)
        {
            base.FormatCustomEventDetails(formatter);
            if (formatter != null) { formatter.AppendLine("Name to authenticate: " + (_nameToAuthenticate == null ? global::System.String.Empty : _nameToAuthenticate)); }
        }
    }

    // ---- Collection / buffer info ---------------------------------------
    public sealed class WebBaseEventCollection : global::System.Collections.ReadOnlyCollectionBase
    {
        public WebBaseEventCollection(global::System.Collections.ICollection events)
        {
            if (events == null) { throw new global::System.ArgumentNullException("events"); }
            foreach (global::System.Object o in events)
            {
                global::System.Web.Management.WebBaseEvent e = o as global::System.Web.Management.WebBaseEvent;
                if (e == null) { throw new global::System.ArgumentException("Collection contains a non-WebBaseEvent element.", "events"); }
                InnerList.Add(e);
            }
        }
        public global::System.Int32 IndexOf(global::System.Web.Management.WebBaseEvent value) { return InnerList.IndexOf(value); }
        public global::System.Boolean Contains(global::System.Web.Management.WebBaseEvent value) { return InnerList.Contains(value); }
        public global::System.Web.Management.WebBaseEvent this[global::System.Int32 index]
        {
            get { return (global::System.Web.Management.WebBaseEvent)InnerList[index]; }
        }
    }

    public enum EventNotificationType
    {
        Regular = 0,
        Urgent = 1,
        Flush = 2,
        Unbuffered = 3,
    }

    public sealed class WebEventBufferFlushInfo
    {
        private readonly global::System.Web.Management.WebBaseEventCollection _events;
        private readonly global::System.DateTime _lastNotificationUtc;
        private readonly global::System.Int32 _eventsDiscarded;
        private readonly global::System.Int32 _eventsInBuffer;
        private readonly global::System.Int32 _notificationSequence;
        private readonly global::System.Web.Management.EventNotificationType _notificationType;
        internal WebEventBufferFlushInfo(global::System.Web.Management.WebBaseEventCollection events,
            global::System.Int32 notificationSequence, global::System.DateTime lastNotificationUtc,
            global::System.Int32 eventsInBuffer, global::System.Int32 eventsDiscarded,
            global::System.Web.Management.EventNotificationType notificationType)
        {
            _events = events;
            _notificationSequence = notificationSequence;
            _lastNotificationUtc = lastNotificationUtc;
            _eventsInBuffer = eventsInBuffer;
            _eventsDiscarded = eventsDiscarded;
            _notificationType = notificationType;
        }
        public global::System.Web.Management.WebBaseEventCollection Events { get { return _events; } }
        public global::System.DateTime LastNotificationUtc { get { return _lastNotificationUtc; } }
        public global::System.Int32 EventsDiscardedSinceLastNotification { get { return _eventsDiscarded; } }
        public global::System.Int32 EventsInBuffer { get { return _eventsInBuffer; } }
        public global::System.Int32 NotificationSequence { get { return _notificationSequence; } }
        public global::System.Web.Management.EventNotificationType NotificationType { get { return _notificationType; } }
    }

    public sealed class MailEventNotificationInfo
    {
        private readonly global::System.Web.Management.WebBaseEventCollection _events;
        private readonly global::System.Web.Management.EventNotificationType _notificationType;
        private readonly global::System.Int32 _eventsInNotification;
        private readonly global::System.Int32 _eventsRemaining;
        private readonly global::System.Int32 _messagesInNotification;
        private readonly global::System.Int32 _eventsInBuffer;
        private readonly global::System.Int32 _eventsDiscardedByBuffer;
        private readonly global::System.Int32 _eventsDiscardedDueToMessageLimit;
        private readonly global::System.Int32 _notificationSequence;
        private readonly global::System.Int32 _messageSequence;
        private readonly global::System.DateTime _lastNotificationUtc;
        private readonly global::System.Net.Mail.MailMessage _message;
        internal MailEventNotificationInfo(global::System.Net.Mail.MailMessage message,
            global::System.Web.Management.WebBaseEventCollection events,
            global::System.Web.Management.EventNotificationType notificationType,
            global::System.Int32 notificationSequence, global::System.DateTime lastNotificationUtc,
            global::System.Int32 eventsInNotification, global::System.Int32 eventsRemaining,
            global::System.Int32 messagesInNotification, global::System.Int32 eventsInBuffer,
            global::System.Int32 eventsDiscardedByBuffer, global::System.Int32 eventsDiscardedDueToMessageLimit,
            global::System.Int32 messageSequence)
        {
            _message = message;
            _events = events;
            _notificationType = notificationType;
            _notificationSequence = notificationSequence;
            _lastNotificationUtc = lastNotificationUtc;
            _eventsInNotification = eventsInNotification;
            _eventsRemaining = eventsRemaining;
            _messagesInNotification = messagesInNotification;
            _eventsInBuffer = eventsInBuffer;
            _eventsDiscardedByBuffer = eventsDiscardedByBuffer;
            _eventsDiscardedDueToMessageLimit = eventsDiscardedDueToMessageLimit;
            _messageSequence = messageSequence;
        }
        public global::System.Web.Management.WebBaseEventCollection Events { get { return _events; } }
        public global::System.Web.Management.EventNotificationType NotificationType { get { return _notificationType; } }
        public global::System.Int32 EventsInNotification { get { return _eventsInNotification; } }
        public global::System.Int32 EventsRemaining { get { return _eventsRemaining; } }
        public global::System.Int32 MessagesInNotification { get { return _messagesInNotification; } }
        public global::System.Int32 EventsInBuffer { get { return _eventsInBuffer; } }
        public global::System.Int32 EventsDiscardedByBuffer { get { return _eventsDiscardedByBuffer; } }
        public global::System.Int32 EventsDiscardedDueToMessageLimit { get { return _eventsDiscardedDueToMessageLimit; } }
        public global::System.Int32 NotificationSequence { get { return _notificationSequence; } }
        public global::System.Int32 MessageSequence { get { return _messageSequence; } }
        public global::System.DateTime LastNotificationUtc { get { return _lastNotificationUtc; } }
        public global::System.Net.Mail.MailMessage Message { get { return _message; } }
    }

    // ---- Provider base hierarchy ----------------------------------------
    public abstract class WebEventProvider : global::System.Configuration.Provider.ProviderBase
    {
        protected WebEventProvider() { }
        public abstract void ProcessEvent(global::System.Web.Management.WebBaseEvent raisedEvent);
        public abstract void Shutdown();
        public abstract void Flush();
    }

    public abstract class BufferedWebEventProvider : global::System.Web.Management.WebEventProvider
    {
        private global::System.Boolean _useBuffering;
        private global::System.String _bufferMode;
        private readonly global::System.Collections.Generic.List<global::System.Web.Management.WebBaseEvent> _buffer
            = new global::System.Collections.Generic.List<global::System.Web.Management.WebBaseEvent>();
        private readonly global::System.Object _bufferLock = new global::System.Object();
        private global::System.Int32 _notificationSequence;
        private global::System.DateTime _lastFlushUtc = global::System.DateTime.UtcNow;

        protected BufferedWebEventProvider() { }
        public override void Initialize(global::System.String name, global::System.Collections.Specialized.NameValueCollection config)
        {
            if (config != null)
            {
                global::System.String buffering = config["buffer"];
                if (buffering != null)
                {
                    _useBuffering = global::System.String.Equals(buffering, "true", global::System.StringComparison.OrdinalIgnoreCase);
                    config.Remove("buffer");
                }
                _bufferMode = config["bufferMode"];
                if (_bufferMode != null) { config.Remove("bufferMode"); }
            }
            base.Initialize(name, config);
        }
        public override void ProcessEvent(global::System.Web.Management.WebBaseEvent eventRaised)
        {
            if (eventRaised == null) { return; }
            if (!_useBuffering)
            {
                global::System.Collections.Generic.List<global::System.Web.Management.WebBaseEvent> single
                    = new global::System.Collections.Generic.List<global::System.Web.Management.WebBaseEvent>();
                single.Add(eventRaised);
                FlushList(single, global::System.Web.Management.EventNotificationType.Unbuffered);
                return;
            }
            lock (_bufferLock) { _buffer.Add(eventRaised); }
        }
        public abstract void ProcessEventFlush(global::System.Web.Management.WebEventBufferFlushInfo flushInfo);
        public override void Flush()
        {
            global::System.Collections.Generic.List<global::System.Web.Management.WebBaseEvent> snapshot;
            lock (_bufferLock)
            {
                if (_buffer.Count == 0) { return; }
                snapshot = new global::System.Collections.Generic.List<global::System.Web.Management.WebBaseEvent>(_buffer);
                _buffer.Clear();
            }
            FlushList(snapshot, global::System.Web.Management.EventNotificationType.Flush);
        }
        private void FlushList(global::System.Collections.Generic.List<global::System.Web.Management.WebBaseEvent> events,
            global::System.Web.Management.EventNotificationType type)
        {
            global::System.Int32 seq = global::System.Threading.Interlocked.Increment(ref _notificationSequence);
            global::System.Web.Management.WebBaseEventCollection coll = new global::System.Web.Management.WebBaseEventCollection(events);
            global::System.Web.Management.WebEventBufferFlushInfo info = new global::System.Web.Management.WebEventBufferFlushInfo(
                coll, seq, _lastFlushUtc, events.Count, 0, type);
            _lastFlushUtc = global::System.DateTime.UtcNow;
            ProcessEventFlush(info);
        }
        public override void Shutdown() { Flush(); }
        public global::System.Boolean UseBuffering { get { return _useBuffering; } }
        public global::System.String BufferMode { get { return _bufferMode; } }
    }

    // ---- Concrete providers ---------------------------------------------

    // Cross-platform event-log provider. On Windows the original writes to the
    // OS Event Log; that API is not portable, so this implementation logs each
    // event's formatted text to standard error.
    public sealed class EventLogWebEventProvider : global::System.Web.Management.WebEventProvider
    {
        internal EventLogWebEventProvider() { }
        public override void Initialize(global::System.String name, global::System.Collections.Specialized.NameValueCollection config)
        {
            base.Initialize(name, config);
        }
        public override void ProcessEvent(global::System.Web.Management.WebBaseEvent eventRaised)
        {
            if (eventRaised == null) { return; }
            try { global::System.Console.Error.WriteLine(eventRaised.ToString(true, true)); }
            catch { }
        }
        public override void Flush() { }
        public override void Shutdown() { }
    }

    // Provider that writes events to System.Diagnostics tracing.
    public sealed class TraceWebEventProvider : global::System.Web.Management.WebEventProvider
    {
        internal TraceWebEventProvider() { }
        public override void Initialize(global::System.String name, global::System.Collections.Specialized.NameValueCollection config)
        {
            base.Initialize(name, config);
        }
        public override void ProcessEvent(global::System.Web.Management.WebBaseEvent eventRaised)
        {
            if (eventRaised == null) { return; }
            global::System.Diagnostics.Trace.WriteLine(eventRaised.ToString(true, true));
        }
        public override void Flush() { }
        public override void Shutdown() { }
    }

    // IIS-integrated trace provider. Routing into the IIS trace pipeline is a
    // Windows/IIS-only facility; in this cross-platform build it behaves like
    // the System.Diagnostics trace provider.
    public sealed class IisTraceWebEventProvider : global::System.Web.Management.WebEventProvider
    {
        public IisTraceWebEventProvider() { }
        public override void Initialize(global::System.String name, global::System.Collections.Specialized.NameValueCollection config)
        {
            base.Initialize(name, config);
        }
        public override void ProcessEvent(global::System.Web.Management.WebBaseEvent eventRaised)
        {
            if (eventRaised == null) { return; }
            global::System.Diagnostics.Trace.WriteLine(eventRaised.ToString(true, true));
        }
        public override void Flush() { }
        public override void Shutdown() { }
    }

    // ---- Mail providers --------------------------------------------------
    public abstract class MailWebEventProvider : global::System.Web.Management.BufferedWebEventProvider
    {
        private global::System.String _from;
        private global::System.String _to;
        private global::System.String _cc;
        private global::System.String _bcc;
        private global::System.String _subjectPrefix;
        private global::System.String _smtpServer;
        private global::System.Int32 _maxEventsPerMessage = 50;
        private global::System.Int32 _messageSequence;

        internal MailWebEventProvider() { }
        public override void Initialize(global::System.String name, global::System.Collections.Specialized.NameValueCollection config)
        {
            if (config != null)
            {
                _from = TakeAttr(config, "from");
                _to = TakeAttr(config, "to");
                _cc = TakeAttr(config, "cc");
                _bcc = TakeAttr(config, "bcc");
                _subjectPrefix = TakeAttr(config, "subjectPrefix");
                _smtpServer = TakeAttr(config, "smtpServer");
                global::System.String maxEvents = TakeAttr(config, "maxEventsPerMessage");
                global::System.Int32 parsed;
                if (!global::System.String.IsNullOrEmpty(maxEvents) &&
                    global::System.Int32.TryParse(maxEvents, out parsed) && parsed > 0)
                {
                    _maxEventsPerMessage = parsed;
                }
            }
            base.Initialize(name, config);
        }
        private static global::System.String TakeAttr(global::System.Collections.Specialized.NameValueCollection config, global::System.String key)
        {
            global::System.String v = config[key];
            if (v != null) { config.Remove(key); }
            return v;
        }
        protected global::System.String From { get { return _from; } }
        protected global::System.String To { get { return _to; } }
        protected global::System.String Cc { get { return _cc; } }
        protected global::System.String Bcc { get { return _bcc; } }
        protected global::System.String SubjectPrefix { get { return _subjectPrefix; } }
        protected global::System.Int32 MaxEventsPerMessage { get { return _maxEventsPerMessage; } }

        public override void ProcessEvent(global::System.Web.Management.WebBaseEvent eventRaised)
        {
            base.ProcessEvent(eventRaised);
        }
        public override void Shutdown() { base.Shutdown(); }
        public override void ProcessEventFlush(global::System.Web.Management.WebEventBufferFlushInfo flushInfo)
        {
            if (flushInfo == null || flushInfo.Events == null) { return; }
            global::System.Int32 total = flushInfo.Events.Count;
            if (total == 0) { return; }
            global::System.Int32 perMsg = _maxEventsPerMessage > 0 ? _maxEventsPerMessage : total;
            global::System.Int32 messagesInNotification = (total + perMsg - 1) / perMsg;
            global::System.Int32 index = 0;
            while (index < total)
            {
                global::System.Collections.Generic.List<global::System.Web.Management.WebBaseEvent> chunk
                    = new global::System.Collections.Generic.List<global::System.Web.Management.WebBaseEvent>();
                global::System.Int32 end = index + perMsg;
                if (end > total) { end = total; }
                for (global::System.Int32 i = index; i < end; i++) { chunk.Add(flushInfo.Events[i]); }
                global::System.Int32 remaining = total - end;
                global::System.Web.Management.WebBaseEventCollection chunkColl = new global::System.Web.Management.WebBaseEventCollection(chunk);
                global::System.Net.Mail.MailMessage message = BuildMessage(chunkColl);
                global::System.Int32 msgSeq = global::System.Threading.Interlocked.Increment(ref _messageSequence);
                global::System.Web.Management.MailEventNotificationInfo info = new global::System.Web.Management.MailEventNotificationInfo(
                    message, chunkColl, flushInfo.NotificationType, flushInfo.NotificationSequence,
                    flushInfo.LastNotificationUtc, chunk.Count, remaining, messagesInNotification,
                    flushInfo.EventsInBuffer, flushInfo.EventsDiscardedSinceLastNotification, 0, msgSeq);
                SendMessage(info);
                index = end;
            }
        }
        // Subclasses customize the message body/subject.
        protected abstract global::System.Net.Mail.MailMessage BuildMessage(global::System.Web.Management.WebBaseEventCollection events);
        protected virtual void SendMessage(global::System.Web.Management.MailEventNotificationInfo info)
        {
            if (info == null || info.Message == null) { return; }
            try
            {
                global::System.Net.Mail.SmtpClient client = global::System.String.IsNullOrEmpty(_smtpServer)
                    ? new global::System.Net.Mail.SmtpClient()
                    : new global::System.Net.Mail.SmtpClient(_smtpServer);
                using (client) { client.Send(info.Message); }
            }
            catch { }
        }
        protected global::System.Net.Mail.MailMessage NewBaseMessage()
        {
            global::System.Net.Mail.MailMessage m = new global::System.Net.Mail.MailMessage();
            if (!global::System.String.IsNullOrEmpty(_from)) { m.From = new global::System.Net.Mail.MailAddress(_from); }
            if (!global::System.String.IsNullOrEmpty(_to)) { m.To.Add(_to); }
            if (!global::System.String.IsNullOrEmpty(_cc)) { m.CC.Add(_cc); }
            if (!global::System.String.IsNullOrEmpty(_bcc)) { m.Bcc.Add(_bcc); }
            return m;
        }
    }

    // Sends a plain-text email with the formatted events.
    public sealed class SimpleMailWebEventProvider : global::System.Web.Management.MailWebEventProvider
    {
        internal SimpleMailWebEventProvider() { }
        public override void Initialize(global::System.String name, global::System.Collections.Specialized.NameValueCollection config)
        {
            base.Initialize(name, config);
        }
        protected override global::System.Net.Mail.MailMessage BuildMessage(global::System.Web.Management.WebBaseEventCollection events)
        {
            global::System.Net.Mail.MailMessage m = NewBaseMessage();
            global::System.Text.StringBuilder body = new global::System.Text.StringBuilder();
            global::System.Int32 count = events == null ? 0 : events.Count;
            for (global::System.Int32 i = 0; i < count; i++)
            {
                body.Append(events[i].ToString(true, true));
                body.Append(global::System.Environment.NewLine);
            }
            global::System.String prefix = SubjectPrefix == null ? global::System.String.Empty : SubjectPrefix;
            m.Subject = prefix + "Web event notification (" + count.ToString(global::System.Globalization.CultureInfo.InvariantCulture) + " events)";
            m.Body = body.ToString();
            m.IsBodyHtml = false;
            return m;
        }
    }

    // Templated mail provider. The original renders an .aspx template; template
    // rendering is out of reach here, so the message body is the formatted
    // events and CurrentNotification exposes the in-flight info during send.
    public sealed class TemplatedMailWebEventProvider : global::System.Web.Management.MailWebEventProvider
    {
        [global::System.ThreadStatic]
        private static global::System.Web.Management.MailEventNotificationInfo s_current;
        private global::System.String _template;
        internal TemplatedMailWebEventProvider() { }
        public override void Initialize(global::System.String name, global::System.Collections.Specialized.NameValueCollection config)
        {
            if (config != null)
            {
                _template = config["template"];
                if (_template != null) { config.Remove("template"); }
            }
            base.Initialize(name, config);
        }
        protected override global::System.Net.Mail.MailMessage BuildMessage(global::System.Web.Management.WebBaseEventCollection events)
        {
            global::System.Net.Mail.MailMessage m = NewBaseMessage();
            global::System.Text.StringBuilder body = new global::System.Text.StringBuilder();
            global::System.Int32 count = events == null ? 0 : events.Count;
            for (global::System.Int32 i = 0; i < count; i++)
            {
                body.Append(events[i].ToString(true, true));
                body.Append(global::System.Environment.NewLine);
            }
            global::System.String prefix = SubjectPrefix == null ? global::System.String.Empty : SubjectPrefix;
            m.Subject = prefix + "Web event notification (templated)";
            m.Body = body.ToString();
            return m;
        }
        protected override void SendMessage(global::System.Web.Management.MailEventNotificationInfo info)
        {
            s_current = info;
            try { base.SendMessage(info); }
            finally { s_current = null; }
        }
        public static global::System.Web.Management.MailEventNotificationInfo CurrentNotification { get { return s_current; } }
    }

    // SQL provider - documented stub. Persisting events requires a configured
    // ASP.NET application-services database (aspnet_WebEvent_Events). That
    // schema/connection is not provisioned in this build, so events are
    // accepted but not persisted; ProcessEventFlush is a no-op sink.
    public class SqlWebEventProvider : global::System.Web.Management.BufferedWebEventProvider
    {
        private global::System.String _connectionStringName;
        protected internal SqlWebEventProvider() { }
        public override void Initialize(global::System.String name, global::System.Collections.Specialized.NameValueCollection config)
        {
            if (config != null)
            {
                _connectionStringName = config["connectionStringName"];
                if (_connectionStringName != null) { config.Remove("connectionStringName"); }
                global::System.String mb = config["maxEventDetailsLength"];
                if (mb != null) { config.Remove("maxEventDetailsLength"); }
            }
            base.Initialize(name, config);
        }
        public override void ProcessEventFlush(global::System.Web.Management.WebEventBufferFlushInfo flushInfo)
        {
            // No database configured: events are dropped (documented stub).
            EventProcessingComplete(flushInfo == null ? null : flushInfo.Events);
        }
        public override void ProcessEvent(global::System.Web.Management.WebBaseEvent eventRaised)
        {
            base.ProcessEvent(eventRaised);
        }
        protected virtual void EventProcessingComplete(global::System.Web.Management.WebBaseEventCollection raisedEvents) { }
        public override void Shutdown() { base.Shutdown(); }
    }

    // WMI provider - documented stub. Publishing events to Windows Management
    // Instrumentation is a Windows-only facility unavailable cross-platform.
    public class WmiWebEventProvider : global::System.Web.Management.WebEventProvider
    {
        public WmiWebEventProvider() { }
        public override void Initialize(global::System.String name, global::System.Collections.Specialized.NameValueCollection config)
        {
            base.Initialize(name, config);
        }
        public override void ProcessEvent(global::System.Web.Management.WebBaseEvent eventRaised)
        {
            // WMI not available on this platform: no-op (documented stub).
        }
        public override void Flush() { }
        public override void Shutdown() { }
    }

    // ---- Manager ---------------------------------------------------------
    public static class WebEventManager
    {
        private static readonly global::System.Object s_lock = new global::System.Object();
        private static global::System.Collections.Generic.Dictionary<global::System.String, global::System.Web.Management.WebEventProvider> s_providers
            = new global::System.Collections.Generic.Dictionary<global::System.String, global::System.Web.Management.WebEventProvider>(global::System.StringComparer.OrdinalIgnoreCase);

        // Internal registration used by the (clean-room) routing path. The full
        // framework builds this set from <healthMonitoring><providers>; those
        // config members are not yet implemented, so registration is exposed
        // internally for hosts/tests to wire providers directly.
        internal static void RegisterProvider(global::System.Web.Management.WebEventProvider provider)
        {
            if (provider == null || provider.Name == null) { return; }
            lock (s_lock) { s_providers[provider.Name] = provider; }
        }
        internal static void RaiseToProviders(global::System.Web.Management.WebBaseEvent eventRaised)
        {
            if (eventRaised == null) { return; }
            global::System.Collections.Generic.List<global::System.Web.Management.WebEventProvider> snapshot;
            lock (s_lock)
            {
                snapshot = new global::System.Collections.Generic.List<global::System.Web.Management.WebEventProvider>(s_providers.Values);
            }
            for (global::System.Int32 i = 0; i < snapshot.Count; i++)
            {
                try { snapshot[i].ProcessEvent(eventRaised); } catch { }
            }
        }
        public static void Flush(global::System.String providerName)
        {
            if (providerName == null) { return; }
            global::System.Web.Management.WebEventProvider provider = null;
            lock (s_lock) { s_providers.TryGetValue(providerName, out provider); }
            if (provider != null)
            {
                try { provider.Flush(); } catch { }
            }
        }
        public static void Flush()
        {
            global::System.Collections.Generic.List<global::System.Web.Management.WebEventProvider> snapshot;
            lock (s_lock)
            {
                snapshot = new global::System.Collections.Generic.List<global::System.Web.Management.WebEventProvider>(s_providers.Values);
            }
            for (global::System.Int32 i = 0; i < snapshot.Count; i++)
            {
                try { snapshot[i].Flush(); } catch { }
            }
        }
    }

    // ---- SQL services / registration (admin tooling) --------------------
    public enum SqlFeatures
    {
        None = 0,
        Membership = 1,
        Profile = 2,
        RoleManager = 4,
        Personalization = 8,
        SqlWebEventProvider = 16,
        All = 1073741855,
    }

    public enum SessionStateType
    {
        Temporary = 0,
        Persisted = 1,
        Custom = 2,
    }

    public sealed class SqlExecutionException : global::System.SystemException
    {
        private readonly global::System.String _server;
        private readonly global::System.String _database;
        private readonly global::System.String _sqlFile;
        private readonly global::System.String _commands;
        private readonly global::System.Data.SqlClient.SqlException _sqlException;
        public SqlExecutionException(global::System.String message, global::System.String server, global::System.String database, global::System.String sqlFile, global::System.String commands, global::System.Data.SqlClient.SqlException sqlException)
            : base(message, sqlException)
        {
            _server = server;
            _database = database;
            _sqlFile = sqlFile;
            _commands = commands;
            _sqlException = sqlException;
        }
        public SqlExecutionException(global::System.String message) : base(message) { }
        public SqlExecutionException(global::System.String message, global::System.Exception innerException) : base(message, innerException) { }
        public SqlExecutionException() : base() { }
        private SqlExecutionException(global::System.Runtime.Serialization.SerializationInfo info, global::System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
            if (info != null)
            {
                _server = info.GetString("_server");
                _database = info.GetString("_database");
                _sqlFile = info.GetString("_sqlFile");
                _commands = info.GetString("_commands");
            }
        }
        public override void GetObjectData(global::System.Runtime.Serialization.SerializationInfo info, global::System.Runtime.Serialization.StreamingContext context)
        {
            base.GetObjectData(info, context);
            if (info != null)
            {
                info.AddValue("_server", _server);
                info.AddValue("_database", _database);
                info.AddValue("_sqlFile", _sqlFile);
                info.AddValue("_commands", _commands);
            }
        }
        public global::System.String Server { get { return _server; } }
        public global::System.String Database { get { return _database; } }
        public global::System.String SqlFile { get { return _sqlFile; } }
        public global::System.String Commands { get { return _commands; } }
        public global::System.Data.SqlClient.SqlException Exception { get { return _sqlException; } }
    }

    // Database installation tooling - documented stub. The original embeds and
    // executes the InstallCommon / InstallWebEventSqlProvider .sql resource
    // scripts against SQL Server; those resources and a SQL Server target are
    // not part of this cross-platform build.
    public static class SqlServices
    {
        private const global::System.String _stub = "System.Web.Management.SqlServices requires SQL Server installation scripts not available in this build.";
        public static void Install(global::System.String server, global::System.String user, global::System.String password, global::System.String database, global::System.Web.Management.SqlFeatures features) { throw new global::System.PlatformNotSupportedException(_stub); }
        public static void Install(global::System.String server, global::System.String database, global::System.Web.Management.SqlFeatures features) { throw new global::System.PlatformNotSupportedException(_stub); }
        public static void Install(global::System.String database, global::System.Web.Management.SqlFeatures features, global::System.String connectionString) { throw new global::System.PlatformNotSupportedException(_stub); }
        public static void Uninstall(global::System.String server, global::System.String user, global::System.String password, global::System.String database, global::System.Web.Management.SqlFeatures features) { throw new global::System.PlatformNotSupportedException(_stub); }
        public static void Uninstall(global::System.String server, global::System.String database, global::System.Web.Management.SqlFeatures features) { throw new global::System.PlatformNotSupportedException(_stub); }
        public static void Uninstall(global::System.String database, global::System.Web.Management.SqlFeatures features, global::System.String connectionString) { throw new global::System.PlatformNotSupportedException(_stub); }
        public static void InstallSessionState(global::System.String server, global::System.String user, global::System.String password, global::System.String customDatabase, global::System.Web.Management.SessionStateType type) { throw new global::System.PlatformNotSupportedException(_stub); }
        public static void InstallSessionState(global::System.String server, global::System.String customDatabase, global::System.Web.Management.SessionStateType type) { throw new global::System.PlatformNotSupportedException(_stub); }
        public static void InstallSessionState(global::System.String customDatabase, global::System.Web.Management.SessionStateType type, global::System.String connectionString) { throw new global::System.PlatformNotSupportedException(_stub); }
        public static void UninstallSessionState(global::System.String server, global::System.String user, global::System.String password, global::System.String customDatabase, global::System.Web.Management.SessionStateType type) { throw new global::System.PlatformNotSupportedException(_stub); }
        public static void UninstallSessionState(global::System.String server, global::System.String customDatabase, global::System.Web.Management.SessionStateType type) { throw new global::System.PlatformNotSupportedException(_stub); }
        public static void UninstallSessionState(global::System.String customDatabase, global::System.Web.Management.SessionStateType type, global::System.String connectionString) { throw new global::System.PlatformNotSupportedException(_stub); }
        public static global::System.String GenerateSessionStateScripts(global::System.Boolean install, global::System.Web.Management.SessionStateType type, global::System.String customDatabase) { throw new global::System.PlatformNotSupportedException(_stub); }
        public static global::System.String GenerateApplicationServicesScripts(global::System.Boolean install, global::System.Web.Management.SqlFeatures features, global::System.String database) { throw new global::System.PlatformNotSupportedException(_stub); }
    }

    public interface IRegiisUtility
    {
        void ProtectedConfigAction(global::System.Int64 actionToPerform, global::System.String firstArgument, global::System.String secondArgument, global::System.String providerName, global::System.String appPath, global::System.String site, global::System.String cspOrLocation, global::System.Int32 keySize, out global::System.IntPtr exception);
        void RegisterSystemWebAssembly(global::System.Int32 doReg, out global::System.IntPtr exception);
        void RegisterAsnetMmcAssembly(global::System.Int32 doReg, global::System.String assemblyName, global::System.String binaryDirectory, out global::System.IntPtr exception);
        void RemoveBrowserCaps(out global::System.IntPtr exception);
    }

    // aspnet_regiis COM utility - documented stub. These operations register
    // ASP.NET with native IIS and manipulate machine-wide protected-config
    // providers via Win32/COM, which are unavailable cross-platform.
    public sealed class RegiisUtility : global::System.Web.Management.IRegiisUtility
    {
        public RegiisUtility() { }
        public void RegisterSystemWebAssembly(global::System.Int32 doReg, out global::System.IntPtr exception) { exception = global::System.IntPtr.Zero; }
        public void RegisterAsnetMmcAssembly(global::System.Int32 doReg, global::System.String typeName, global::System.String binaryDirectory, out global::System.IntPtr exception) { exception = global::System.IntPtr.Zero; }
        public void ProtectedConfigAction(global::System.Int64 options, global::System.String firstArgument, global::System.String secondArgument, global::System.String providerName, global::System.String appPath, global::System.String site, global::System.String cspOrLocation, global::System.Int32 keySize, out global::System.IntPtr exception) { exception = global::System.IntPtr.Zero; }
        public void RemoveBrowserCaps(out global::System.IntPtr exception) { exception = global::System.IntPtr.Zero; }
    }
}
