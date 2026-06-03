// clean-room System.Web.SessionState implementation (Tier-3 InProc).
// Built from public signatures + documented ASP.NET behavior + RFCs only.
// No LINQ. Out-of-proc (StateServer/SQLServer/Custom) providers are documented stubs.
#nullable disable
#pragma warning disable

namespace System.Web.SessionState
{
    public interface IPartialSessionState
    {
        global::System.Collections.Generic.IList<global::System.String> PartialSessionStateKeys { get; }
    }
    public interface IReadOnlySessionState : global::System.Web.SessionState.IRequiresSessionState
    {
    }
    public interface IRequiresSessionState
    {
    }
    public enum SessionStateActions
    {
        None = 0,
        InitializeItem = 1,
    }
    public abstract class SessionStateStoreProviderBase : global::System.Configuration.Provider.ProviderBase
    {
        // Documented TODO: out-of-proc/custom store providers are not part of the Tier-3 InProc target.
        protected SessionStateStoreProviderBase() { }
        public abstract void Dispose();
        public abstract global::System.Boolean SetItemExpireCallback(global::System.Web.SessionState.SessionStateItemExpireCallback expireCallback);
        public abstract void InitializeRequest(global::System.Web.HttpContext context);
        public abstract global::System.Web.SessionState.SessionStateStoreData GetItem(global::System.Web.HttpContext context, global::System.String id, out global::System.Boolean locked, out global::System.TimeSpan lockAge, out global::System.Object lockId, out global::System.Web.SessionState.SessionStateActions actions);
        public abstract global::System.Web.SessionState.SessionStateStoreData GetItemExclusive(global::System.Web.HttpContext context, global::System.String id, out global::System.Boolean locked, out global::System.TimeSpan lockAge, out global::System.Object lockId, out global::System.Web.SessionState.SessionStateActions actions);
        public abstract void ReleaseItemExclusive(global::System.Web.HttpContext context, global::System.String id, global::System.Object lockId);
        public abstract void SetAndReleaseItemExclusive(global::System.Web.HttpContext context, global::System.String id, global::System.Web.SessionState.SessionStateStoreData item, global::System.Object lockId, global::System.Boolean newItem);
        public abstract void RemoveItem(global::System.Web.HttpContext context, global::System.String id, global::System.Object lockId, global::System.Web.SessionState.SessionStateStoreData item);
        public abstract void ResetItemTimeout(global::System.Web.HttpContext context, global::System.String id);
        public abstract global::System.Web.SessionState.SessionStateStoreData CreateNewStoreData(global::System.Web.HttpContext context, global::System.Int32 timeout);
        public abstract void CreateUninitializedItem(global::System.Web.HttpContext context, global::System.String id, global::System.Int32 timeout);
        public abstract void EndRequest(global::System.Web.HttpContext context);
    }
    public class SessionStateStoreData
    {
        private readonly global::System.Web.SessionState.ISessionStateItemCollection _items;
        private readonly global::System.Web.HttpStaticObjectsCollection _staticObjects;
        private global::System.Int32 _timeout;

        public SessionStateStoreData(global::System.Web.SessionState.ISessionStateItemCollection sessionItems, global::System.Web.HttpStaticObjectsCollection staticObjects, global::System.Int32 timeout)
        {
            _items = sessionItems;
            _staticObjects = staticObjects;
            _timeout = timeout;
        }
        public virtual global::System.Web.SessionState.ISessionStateItemCollection Items { get { return _items; } }
        public virtual global::System.Web.HttpStaticObjectsCollection StaticObjects { get { return _staticObjects; } }
        public virtual global::System.Int32 Timeout { get { return _timeout; } set { _timeout = value; } }
    }
    public interface ISessionStateModule : global::System.Web.IHttpModule
    {
        void ReleaseSessionState(global::System.Web.HttpContext context);
        global::System.Threading.Tasks.Task ReleaseSessionStateAsync(global::System.Web.HttpContext context);
    }
    public interface ISessionIDManager
    {
        global::System.Boolean InitializeRequest(global::System.Web.HttpContext context, global::System.Boolean suppressAutoDetectRedirect, out global::System.Boolean supportSessionIDReissue);
        global::System.String GetSessionID(global::System.Web.HttpContext context);
        global::System.String CreateSessionID(global::System.Web.HttpContext context);
        void SaveSessionID(global::System.Web.HttpContext context, global::System.String id, out global::System.Boolean redirected, out global::System.Boolean cookieAdded);
        void RemoveSessionID(global::System.Web.HttpContext context);
        global::System.Boolean Validate(global::System.String id);
        void Initialize();
    }
    public class SessionIDManager : global::System.Web.SessionState.ISessionIDManager
    {
        // ASP.NET session ids are 24 URL-safe characters drawn from a fixed alphabet.
        private const global::System.Int32 _idLength = 24;
        private const global::System.String _legalChars = "abcdefghijklmnopqrstuvwxyz0123456789";
        private const global::System.Int32 _numBytes = 15;

        private global::System.String _cookieName = "ASP.NET_SessionId";

        public SessionIDManager() { }

        public void Initialize()
        {
            global::System.Web.SessionState.SessionStateSectionAccess.Read(out this._cookieName);
        }

        public virtual global::System.Boolean Validate(global::System.String id)
        {
            if (id == null)
            {
                return false;
            }
            if (id.Length != _idLength)
            {
                return false;
            }
            for (global::System.Int32 i = 0; i < id.Length; i++)
            {
                global::System.Char c = id[i];
                global::System.Boolean ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                if (!ok)
                {
                    return false;
                }
            }
            return true;
        }

        public virtual global::System.String Encode(global::System.String id)
        {
            // InProc uses the raw id as the cookie value; no obfuscation transform.
            return id;
        }

        public virtual global::System.String Decode(global::System.String id)
        {
            return id;
        }

        public global::System.Boolean InitializeRequest(global::System.Web.HttpContext context, global::System.Boolean suppressAutoDetectRedirect, out global::System.Boolean supportSessionIDReissue)
        {
            // InProc cookie-based mode never needs a cookieless auto-detect redirect.
            supportSessionIDReissue = true;
            return false;
        }

        public global::System.String GetSessionID(global::System.Web.HttpContext context)
        {
            if (context == null || context.Request == null)
            {
                return null;
            }
            global::System.Web.HttpCookie cookie = context.Request.Cookies[this._cookieName];
            if (cookie == null)
            {
                return null;
            }
            global::System.String id = Decode(cookie.Value);
            if (!Validate(id))
            {
                return null;
            }
            return id;
        }

        public virtual global::System.String CreateSessionID(global::System.Web.HttpContext context)
        {
            global::System.Byte[] buffer = new global::System.Byte[_numBytes];
            using (global::System.Security.Cryptography.RandomNumberGenerator rng = global::System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(buffer);
            }
            // Map random bytes onto the legal lowercase-alnum alphabet for a stable 24-char id.
            global::System.Char[] chars = new global::System.Char[_idLength];
            global::System.Int32 bitBuffer = 0;
            global::System.Int32 bitCount = 0;
            global::System.Int32 bi = 0;
            for (global::System.Int32 i = 0; i < _idLength; i++)
            {
                if (bitCount < 6)
                {
                    if (bi < buffer.Length)
                    {
                        bitBuffer |= (buffer[bi] & 0xFF) << bitCount;
                        bi++;
                        bitCount += 8;
                    }
                    else
                    {
                        // Refill deterministically if we run short (should not happen with 15 bytes).
                        bitBuffer |= (i * 31 + 7) << bitCount;
                        bitCount += 8;
                    }
                }
                global::System.Int32 index = bitBuffer & 0x1F; // 5 bits -> 0..31
                bitBuffer >>= 5;
                bitCount -= 5;
                chars[i] = _legalChars[index % _legalChars.Length];
            }
            return new global::System.String(chars);
        }

        public void SaveSessionID(global::System.Web.HttpContext context, global::System.String id, out global::System.Boolean redirected, out global::System.Boolean cookieAdded)
        {
            redirected = false;
            cookieAdded = false;
            if (context == null || context.Response == null)
            {
                return;
            }
            if (!Validate(id))
            {
                throw new global::System.Web.HttpException("Invalid session id.");
            }
            global::System.Web.HttpCookie cookie = new global::System.Web.HttpCookie(this._cookieName, Encode(id));
            cookie.Path = "/";
            cookie.HttpOnly = true;
            context.Response.Cookies.Add(cookie);
            cookieAdded = true;
        }

        public void RemoveSessionID(global::System.Web.HttpContext context)
        {
            if (context == null || context.Response == null)
            {
                return;
            }
            // Expire the cookie at the client.
            global::System.Web.HttpCookie cookie = new global::System.Web.HttpCookie(this._cookieName, global::System.String.Empty);
            cookie.Path = "/";
            cookie.Expires = new global::System.DateTime(1990, 1, 1);
            context.Response.Cookies.Add(cookie);
        }

        public static global::System.Int32 SessionIDMaxLength { get { return 80; } }
    }

    // Internal helper to read the <sessionState> config defensively (no throw if absent).
    internal static class SessionStateSectionAccess
    {
        internal static void Read(out global::System.String cookieName)
        {
            global::System.Web.SessionState.SessionStateMode mode;
            global::System.Int32 timeoutMinutes;
            ReadAll(out cookieName, out mode, out timeoutMinutes);
        }

        internal static void ReadAll(out global::System.String cookieName, out global::System.Web.SessionState.SessionStateMode mode, out global::System.Int32 timeoutMinutes)
        {
            cookieName = "ASP.NET_SessionId";
            mode = global::System.Web.SessionState.SessionStateMode.InProc;
            timeoutMinutes = 20;
            try
            {
                global::System.Object o = global::System.Web.Configuration.WebConfigurationManager.GetSection("system.web/sessionState");
                global::System.Web.Configuration.SessionStateSection s = o as global::System.Web.Configuration.SessionStateSection;
                if (s != null)
                {
                    if (!global::System.String.IsNullOrEmpty(s.CookieName))
                    {
                        cookieName = s.CookieName;
                    }
                    mode = s.Mode;
                    global::System.TimeSpan t = s.Timeout;
                    if (t > global::System.TimeSpan.Zero)
                    {
                        timeoutMinutes = (global::System.Int32)t.TotalMinutes;
                        if (timeoutMinutes <= 0)
                        {
                            timeoutMinutes = 20;
                        }
                    }
                }
            }
            catch
            {
                // Missing/invalid config falls back to documented defaults.
            }
        }
    }

    public enum SessionStateMode
    {
        Off = 0,
        InProc = 1,
        StateServer = 2,
        SQLServer = 3,
        Custom = 4,
    }
    public interface IHttpSessionState
    {
        void Abandon();
        void Add(global::System.String name, global::System.Object value);
        void Remove(global::System.String name);
        void RemoveAt(global::System.Int32 index);
        void Clear();
        void RemoveAll();
        global::System.Collections.IEnumerator GetEnumerator();
        void CopyTo(global::System.Array array, global::System.Int32 index);
        global::System.String SessionID { get; }
        global::System.Int32 Timeout { get; set; }
        global::System.Boolean IsNewSession { get; }
        global::System.Web.SessionState.SessionStateMode Mode { get; }
        global::System.Boolean IsCookieless { get; }
        global::System.Web.HttpCookieMode CookieMode { get; }
        global::System.Int32 LCID { get; set; }
        global::System.Int32 CodePage { get; set; }
        global::System.Web.HttpStaticObjectsCollection StaticObjects { get; }
        global::System.Object this[global::System.String name] { get; set; }
        global::System.Object this[global::System.Int32 index] { get; set; }
        global::System.Int32 Count { get; }
        global::System.Collections.Specialized.NameObjectCollectionBase.KeysCollection Keys { get; }
        global::System.Object SyncRoot { get; }
        global::System.Boolean IsReadOnly { get; }
        global::System.Boolean IsSynchronized { get; }
    }
    public sealed class HttpSessionState : global::System.Collections.ICollection, global::System.Collections.IEnumerable
    {
        private readonly global::System.Web.SessionState.HttpSessionStateContainer _container;

        // Constructed by the module via the internal ctor; never directly by user code.
        internal HttpSessionState(global::System.Web.SessionState.HttpSessionStateContainer container)
        {
            if (container == null)
            {
                throw new global::System.ArgumentNullException("container");
            }
            _container = container;
        }

        internal global::System.Web.SessionState.HttpSessionStateContainer Container { get { return _container; } }

        public void Abandon() { _container.Abandon(); }
        public void Add(global::System.String name, global::System.Object value) { _container.Add(name, value); }
        public void Remove(global::System.String name) { _container.Remove(name); }
        public void RemoveAt(global::System.Int32 index) { _container.RemoveAt(index); }
        public void Clear() { _container.Clear(); }
        public void RemoveAll() { _container.RemoveAll(); }
        public global::System.Collections.IEnumerator GetEnumerator() { return _container.GetEnumerator(); }
        public void CopyTo(global::System.Array array, global::System.Int32 index) { _container.CopyTo(array, index); }
        public global::System.String SessionID { get { return _container.SessionID; } }
        public global::System.Int32 Timeout { get { return _container.Timeout; } set { _container.Timeout = value; } }
        public global::System.Boolean IsNewSession { get { return _container.IsNewSession; } }
        public global::System.Web.SessionState.SessionStateMode Mode { get { return _container.Mode; } }
        public global::System.Boolean IsCookieless { get { return _container.IsCookieless; } }
        public global::System.Web.HttpCookieMode CookieMode { get { return _container.CookieMode; } }
        public global::System.Int32 LCID { get { return _container.LCID; } set { _container.LCID = value; } }
        public global::System.Int32 CodePage { get { return _container.CodePage; } set { _container.CodePage = value; } }
        public global::System.Web.SessionState.HttpSessionState Contents { get { return this; } }
        public global::System.Web.HttpStaticObjectsCollection StaticObjects { get { return _container.StaticObjects; } }
        public global::System.Object this[global::System.String name] { get { return _container[name]; } set { _container[name] = value; } }
        public global::System.Object this[global::System.Int32 index] { get { return _container[index]; } set { _container[index] = value; } }
        public global::System.Int32 Count { get { return _container.Count; } }
        public global::System.Collections.Specialized.NameObjectCollectionBase.KeysCollection Keys { get { return _container.Keys; } }
        public global::System.Object SyncRoot { get { return _container.SyncRoot; } }
        public global::System.Boolean IsReadOnly { get { return _container.IsReadOnly; } }
        public global::System.Boolean IsSynchronized { get { return _container.IsSynchronized; } }
    }
    public enum SessionStateBehavior
    {
        Default = 0,
        Required = 1,
        ReadOnly = 2,
        Disabled = 3,
    }
    public class HttpSessionStateContainer : global::System.Web.SessionState.IHttpSessionState
    {
        private readonly global::System.String _id;
        private global::System.Web.SessionState.ISessionStateItemCollection _sessionItems;
        private readonly global::System.Web.HttpStaticObjectsCollection _staticObjects;
        private global::System.Int32 _timeout;
        private global::System.Boolean _isNewSession;
        private readonly global::System.Web.HttpCookieMode _cookieMode;
        private readonly global::System.Web.SessionState.SessionStateMode _mode;
        private readonly global::System.Boolean _isReadonly;
        private global::System.Int32 _lcid = -1;
        private global::System.Int32 _codePage = -1;
        private global::System.Boolean _abandoned;
        private readonly global::System.Object _syncRoot = new global::System.Object();

        public HttpSessionStateContainer(global::System.String id, global::System.Web.SessionState.ISessionStateItemCollection sessionItems, global::System.Web.HttpStaticObjectsCollection staticObjects, global::System.Int32 timeout, global::System.Boolean newSession, global::System.Web.HttpCookieMode cookieMode, global::System.Web.SessionState.SessionStateMode mode, global::System.Boolean isReadonly)
        {
            _id = id;
            _sessionItems = sessionItems != null ? sessionItems : new global::System.Web.SessionState.SessionStateItemCollection();
            _staticObjects = staticObjects != null ? staticObjects : new global::System.Web.HttpStaticObjectsCollection();
            _timeout = timeout;
            _isNewSession = newSession;
            _cookieMode = cookieMode;
            _mode = mode;
            _isReadonly = isReadonly;
        }

        internal global::System.Web.SessionState.ISessionStateItemCollection SessionItems { get { return _sessionItems; } }

        private void EnsureWritable()
        {
            if (_isReadonly)
            {
                throw new global::System.NotSupportedException("Session state is read-only for this request.");
            }
        }

        public void Abandon() { _abandoned = true; }

        public void Add(global::System.String name, global::System.Object value)
        {
            EnsureWritable();
            _sessionItems[name] = value;
        }
        public void Remove(global::System.String name)
        {
            EnsureWritable();
            _sessionItems.Remove(name);
        }
        public void RemoveAt(global::System.Int32 index)
        {
            EnsureWritable();
            _sessionItems.RemoveAt(index);
        }
        public void Clear()
        {
            EnsureWritable();
            _sessionItems.Clear();
        }
        public void RemoveAll()
        {
            EnsureWritable();
            _sessionItems.Clear();
        }
        public global::System.Collections.IEnumerator GetEnumerator()
        {
            // Enumerate the keys, matching the framework's HttpSessionState enumerator semantics.
            return _sessionItems.Keys.GetEnumerator();
        }
        public void CopyTo(global::System.Array array, global::System.Int32 index)
        {
            if (array == null)
            {
                throw new global::System.ArgumentNullException("array");
            }
            global::System.Collections.Specialized.NameObjectCollectionBase.KeysCollection keys = _sessionItems.Keys;
            global::System.Int32 i = index;
            foreach (global::System.Object key in keys)
            {
                array.SetValue(key, i);
                i++;
            }
        }
        public global::System.String SessionID { get { return _id; } }
        public global::System.Int32 Timeout
        {
            get { return _timeout; }
            set
            {
                if (value <= 0)
                {
                    throw new global::System.ArgumentException("Timeout must be greater than zero.", "value");
                }
                _timeout = value;
            }
        }
        public global::System.Boolean IsNewSession { get { return _isNewSession; } }
        public global::System.Web.SessionState.SessionStateMode Mode { get { return _mode; } }
        public global::System.Boolean IsCookieless { get { return _cookieMode == global::System.Web.HttpCookieMode.UseUri; } }
        public global::System.Web.HttpCookieMode CookieMode { get { return _cookieMode; } }
        public global::System.Int32 LCID
        {
            get { return _lcid != -1 ? _lcid : global::System.Globalization.CultureInfo.CurrentCulture.LCID; }
            set { _lcid = value; }
        }
        public global::System.Int32 CodePage
        {
            get { return _codePage != -1 ? _codePage : global::System.Text.Encoding.UTF8.CodePage; }
            set { _codePage = value; }
        }
        public global::System.Boolean IsAbandoned { get { return _abandoned; } }
        public global::System.Web.HttpStaticObjectsCollection StaticObjects { get { return _staticObjects; } }
        public global::System.Object this[global::System.String name]
        {
            get { return _sessionItems[name]; }
            set { EnsureWritable(); _sessionItems[name] = value; }
        }
        public global::System.Object this[global::System.Int32 index]
        {
            get { return _sessionItems[index]; }
            set { EnsureWritable(); _sessionItems[index] = value; }
        }
        public global::System.Int32 Count { get { return _sessionItems.Count; } }
        public global::System.Collections.Specialized.NameObjectCollectionBase.KeysCollection Keys { get { return _sessionItems.Keys; } }
        public global::System.Object SyncRoot { get { return _syncRoot; } }
        public global::System.Boolean IsReadOnly { get { return _isReadonly; } }
        public global::System.Boolean IsSynchronized { get { return false; } }

        // Module hook: mark the contained session as no-longer-new after the first request completes.
        internal void MarkOld() { _isNewSession = false; }
    }
    public interface ISessionStateItemCollection : global::System.Collections.ICollection, global::System.Collections.IEnumerable
    {
        void Remove(global::System.String name);
        void RemoveAt(global::System.Int32 index);
        void Clear();
        global::System.Object this[global::System.String name] { get; set; }
        global::System.Object this[global::System.Int32 index] { get; set; }
        global::System.Collections.Specialized.NameObjectCollectionBase.KeysCollection Keys { get; }
        global::System.Boolean Dirty { get; set; }
    }
    public sealed class SessionStateItemCollection : global::System.Collections.Specialized.NameObjectCollectionBase, global::System.Web.SessionState.ISessionStateItemCollection, global::System.Collections.ICollection, global::System.Collections.IEnumerable
    {
        private global::System.Boolean _dirty;

        public SessionStateItemCollection()
            : base(global::System.StringComparer.OrdinalIgnoreCase)
        {
        }
        public void Remove(global::System.String name)
        {
            if (name == null)
            {
                throw new global::System.ArgumentNullException("name");
            }
            BaseRemove(name);
            _dirty = true;
        }
        public void RemoveAt(global::System.Int32 index)
        {
            BaseRemoveAt(index);
            _dirty = true;
        }
        public void Clear()
        {
            BaseClear();
            _dirty = true;
        }
        public override global::System.Collections.IEnumerator GetEnumerator()
        {
            return base.Keys.GetEnumerator();
        }
        public void Serialize(global::System.IO.BinaryWriter writer)
        {
            if (writer == null)
            {
                throw new global::System.ArgumentNullException("writer");
            }
            global::System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bf = new global::System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            global::System.String[] keys = BaseGetAllKeys();
            writer.Write(keys.Length);
            for (global::System.Int32 i = 0; i < keys.Length; i++)
            {
                writer.Write(keys[i] != null ? keys[i] : global::System.String.Empty);
                global::System.Object value = BaseGet(keys[i]);
                if (value == null)
                {
                    writer.Write(false);
                }
                else
                {
                    writer.Write(true);
                    using (global::System.IO.MemoryStream ms = new global::System.IO.MemoryStream())
                    {
#pragma warning disable SYSLIB0011
                        bf.Serialize(ms, value);
#pragma warning restore SYSLIB0011
                        global::System.Byte[] bytes = ms.ToArray();
                        writer.Write(bytes.Length);
                        writer.Write(bytes);
                    }
                }
            }
        }
        public static global::System.Web.SessionState.SessionStateItemCollection Deserialize(global::System.IO.BinaryReader reader)
        {
            if (reader == null)
            {
                throw new global::System.ArgumentNullException("reader");
            }
            global::System.Web.SessionState.SessionStateItemCollection result = new global::System.Web.SessionState.SessionStateItemCollection();
            global::System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bf = new global::System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            global::System.Int32 count = reader.ReadInt32();
            for (global::System.Int32 i = 0; i < count; i++)
            {
                global::System.String key = reader.ReadString();
                global::System.Boolean hasValue = reader.ReadBoolean();
                global::System.Object value = null;
                if (hasValue)
                {
                    global::System.Int32 len = reader.ReadInt32();
                    global::System.Byte[] bytes = reader.ReadBytes(len);
                    using (global::System.IO.MemoryStream ms = new global::System.IO.MemoryStream(bytes))
                    {
#pragma warning disable SYSLIB0011
                        value = bf.Deserialize(ms);
#pragma warning restore SYSLIB0011
                    }
                }
                result.BaseSet(key, value);
            }
            result._dirty = false;
            return result;
        }
        public global::System.Boolean Dirty { get { return _dirty; } set { _dirty = value; } }
        public global::System.Object this[global::System.String name]
        {
            get
            {
                if (name == null)
                {
                    throw new global::System.ArgumentNullException("name");
                }
                return BaseGet(name);
            }
            set
            {
                if (name == null)
                {
                    throw new global::System.ArgumentNullException("name");
                }
                BaseSet(name, value);
                _dirty = true;
            }
        }
        public global::System.Object this[global::System.Int32 index]
        {
            get { return BaseGet(index); }
            set { BaseSet(index, value); _dirty = true; }
        }
        public override global::System.Collections.Specialized.NameObjectCollectionBase.KeysCollection Keys { get { return base.Keys; } }
    }
    public delegate void SessionStateItemExpireCallback(global::System.String id, global::System.Web.SessionState.SessionStateStoreData item);

    // Process-wide InProc store: thread-safe id -> entry map with sliding-expiration eviction.
    internal sealed class InProcSessionStateStore
    {
        internal sealed class Entry
        {
            internal global::System.Web.SessionState.ISessionStateItemCollection Items;
            internal global::System.Web.HttpStaticObjectsCollection StaticObjects;
            internal global::System.Int32 TimeoutMinutes;
            internal global::System.DateTime ExpiresUtc;
            internal global::System.Boolean IsNew;
        }

        private static readonly global::System.Web.SessionState.InProcSessionStateStore _instance = new global::System.Web.SessionState.InProcSessionStateStore();
        internal static global::System.Web.SessionState.InProcSessionStateStore Instance { get { return _instance; } }

        private readonly global::System.Collections.Generic.Dictionary<global::System.String, global::System.Web.SessionState.InProcSessionStateStore.Entry> _map =
            new global::System.Collections.Generic.Dictionary<global::System.String, global::System.Web.SessionState.InProcSessionStateStore.Entry>(global::System.StringComparer.Ordinal);
        private readonly global::System.Object _lock = new global::System.Object();

        // Returns an existing (non-expired) entry, refreshing its sliding window, or null.
        internal Entry Get(global::System.String id)
        {
            if (id == null)
            {
                return null;
            }
            lock (_lock)
            {
                Entry e;
                if (_map.TryGetValue(id, out e))
                {
                    if (e.ExpiresUtc <= global::System.DateTime.UtcNow)
                    {
                        _map.Remove(id);
                        return null;
                    }
                    // Sliding refresh on access.
                    e.ExpiresUtc = global::System.DateTime.UtcNow.AddMinutes(e.TimeoutMinutes);
                    e.IsNew = false;
                    return e;
                }
                return null;
            }
        }

        internal Entry Create(global::System.String id, global::System.Int32 timeoutMinutes)
        {
            Entry e = new Entry();
            e.Items = new global::System.Web.SessionState.SessionStateItemCollection();
            e.StaticObjects = new global::System.Web.HttpStaticObjectsCollection();
            e.TimeoutMinutes = timeoutMinutes > 0 ? timeoutMinutes : 20;
            e.ExpiresUtc = global::System.DateTime.UtcNow.AddMinutes(e.TimeoutMinutes);
            e.IsNew = true;
            lock (_lock)
            {
                _map[id] = e;
            }
            return e;
        }

        internal void Save(global::System.String id, Entry e)
        {
            if (id == null || e == null)
            {
                return;
            }
            lock (_lock)
            {
                e.ExpiresUtc = global::System.DateTime.UtcNow.AddMinutes(e.TimeoutMinutes > 0 ? e.TimeoutMinutes : 20);
                e.IsNew = false;
                _map[id] = e;
            }
        }

        internal void Remove(global::System.String id)
        {
            if (id == null)
            {
                return;
            }
            lock (_lock)
            {
                _map.Remove(id);
            }
        }

        // Opportunistic sweep of expired entries.
        internal void Sweep()
        {
            global::System.DateTime now = global::System.DateTime.UtcNow;
            lock (_lock)
            {
                global::System.Collections.Generic.List<global::System.String> dead = null;
                foreach (global::System.Collections.Generic.KeyValuePair<global::System.String, Entry> kvp in _map)
                {
                    if (kvp.Value.ExpiresUtc <= now)
                    {
                        if (dead == null)
                        {
                            dead = new global::System.Collections.Generic.List<global::System.String>();
                        }
                        dead.Add(kvp.Key);
                    }
                }
                if (dead != null)
                {
                    for (global::System.Int32 i = 0; i < dead.Count; i++)
                    {
                        _map.Remove(dead[i]);
                    }
                }
            }
        }
    }

    public sealed class SessionStateModule : global::System.Web.SessionState.ISessionStateModule, global::System.Web.IHttpModule
    {
        // Per-request scratch keys stored in HttpContext.Items.
        private const global::System.String _ctxIdKey = "__SessionStateModule_Id";
        private const global::System.String _ctxNewKey = "__SessionStateModule_IsNew";
        private const global::System.String _ctxContainerKey = "__SessionStateModule_Container";

        private readonly global::System.Web.SessionState.SessionIDManager _idManager = new global::System.Web.SessionState.SessionIDManager();
        private global::System.Int32 _timeoutMinutes = 20;
        private global::System.Web.SessionState.SessionStateMode _mode = global::System.Web.SessionState.SessionStateMode.InProc;

        public SessionStateModule() { }

        public void Init(global::System.Web.HttpApplication app)
        {
            if (app == null)
            {
                throw new global::System.ArgumentNullException("app");
            }
            global::System.String cookieName;
            global::System.Web.SessionState.SessionStateSectionAccess.ReadAll(out cookieName, out this._mode, out this._timeoutMinutes);
            this._idManager.Initialize();
            app.AcquireRequestState += new global::System.EventHandler(this.OnAcquireRequestState);
            app.ReleaseRequestState += new global::System.EventHandler(this.OnReleaseRequestState);
            app.EndRequest += new global::System.EventHandler(this.OnEndRequest);
        }

        public void Dispose() { }

        private void OnAcquireRequestState(global::System.Object sender, global::System.EventArgs e)
        {
            global::System.Web.HttpApplication app = sender as global::System.Web.HttpApplication;
            if (app == null)
            {
                return;
            }
            global::System.Web.HttpContext context = app.Context;
            if (context == null)
            {
                return;
            }
            // Off mode -> no session published.
            if (this._mode == global::System.Web.SessionState.SessionStateMode.Off)
            {
                return;
            }
            // Out-of-proc modes are not implemented for Tier 3; the InProc store backs all requests.
            global::System.Web.SessionState.InProcSessionStateStore store = global::System.Web.SessionState.InProcSessionStateStore.Instance;
            store.Sweep();

            global::System.Boolean isNew = false;
            global::System.String id = this._idManager.GetSessionID(context);
            global::System.Web.SessionState.InProcSessionStateStore.Entry entry = null;
            if (id != null)
            {
                entry = store.Get(id);
            }
            if (entry == null)
            {
                // No valid existing session: create id + entry and emit the cookie.
                id = this._idManager.CreateSessionID(context);
                global::System.Boolean redirected;
                global::System.Boolean cookieAdded;
                this._idManager.SaveSessionID(context, id, out redirected, out cookieAdded);
                entry = store.Create(id, this._timeoutMinutes);
                isNew = true;
            }

            global::System.Web.SessionState.HttpSessionStateContainer container = new global::System.Web.SessionState.HttpSessionStateContainer(
                id,
                entry.Items,
                entry.StaticObjects,
                entry.TimeoutMinutes,
                isNew,
                global::System.Web.HttpCookieMode.UseCookies,
                global::System.Web.SessionState.SessionStateMode.InProc,
                false);

            global::System.Web.SessionState.HttpSessionState session = new global::System.Web.SessionState.HttpSessionState(container);
            context.SetSessionStateInstance(session);

            // Stash per-request bookkeeping for release.
            context.Items[_ctxIdKey] = id;
            context.Items[_ctxNewKey] = isNew;
            context.Items[_ctxContainerKey] = container;

            if (isNew)
            {
                this.OnStart(context);
            }
        }

        private void OnReleaseRequestState(global::System.Object sender, global::System.EventArgs e)
        {
            global::System.Web.HttpApplication app = sender as global::System.Web.HttpApplication;
            if (app == null)
            {
                return;
            }
            this.ReleaseSessionState(app.Context);
        }

        private void OnEndRequest(global::System.Object sender, global::System.EventArgs e)
        {
            global::System.Web.HttpApplication app = sender as global::System.Web.HttpApplication;
            if (app != null && app.Context != null)
            {
                // Ensure release happened even if ReleaseRequestState was bypassed.
                this.ReleaseSessionState(app.Context);
            }
        }

        public void ReleaseSessionState(global::System.Web.HttpContext context)
        {
            if (context == null)
            {
                return;
            }
            global::System.Object containerObj = context.Items[_ctxContainerKey];
            global::System.Web.SessionState.HttpSessionStateContainer container = containerObj as global::System.Web.SessionState.HttpSessionStateContainer;
            if (container == null)
            {
                return;
            }
            global::System.String id = context.Items[_ctxIdKey] as global::System.String;
            global::System.Web.SessionState.InProcSessionStateStore store = global::System.Web.SessionState.InProcSessionStateStore.Instance;

            if (container.IsAbandoned)
            {
                if (id != null)
                {
                    store.Remove(id);
                    this._idManager.RemoveSessionID(context);
                    this.OnEnd(id, container, context);
                }
            }
            else if (id != null)
            {
                // InProc holds object references directly; persist by refreshing the sliding window.
                global::System.Web.SessionState.InProcSessionStateStore.Entry entry = new global::System.Web.SessionState.InProcSessionStateStore.Entry();
                entry.Items = container.SessionItems;
                entry.StaticObjects = container.StaticObjects;
                entry.TimeoutMinutes = container.Timeout;
                store.Save(id, entry);
            }

            // Avoid double-processing on EndRequest after ReleaseRequestState.
            context.Items.Remove(_ctxContainerKey);
        }

        public global::System.Threading.Tasks.Task ReleaseSessionStateAsync(global::System.Web.HttpContext context)
        {
            this.ReleaseSessionState(context);
            return global::System.Threading.Tasks.Task.CompletedTask;
        }

        private void OnStart(global::System.Web.HttpContext context)
        {
            global::System.EventHandler h = this.Start;
            if (h != null)
            {
                h(this, global::System.EventArgs.Empty);
            }
        }

        private void OnEnd(global::System.String id, global::System.Web.SessionState.HttpSessionStateContainer container, global::System.Web.HttpContext context)
        {
            global::System.EventHandler h = this.End;
            if (h != null)
            {
                h(this, global::System.EventArgs.Empty);
            }
        }

        public event global::System.EventHandler Start;
        public event global::System.EventHandler End;
    }

    public static class SessionStateUtility
    {
        public static void RaiseSessionEnd(global::System.Web.SessionState.IHttpSessionState session, global::System.Object eventSource, global::System.EventArgs eventArgs)
        {
            // Session_OnEnd is raised by the module; no global Application handler wiring at this tier.
        }
        public static void AddHttpSessionStateToContext(global::System.Web.HttpContext context, global::System.Web.SessionState.IHttpSessionState container)
        {
            if (context == null)
            {
                throw new global::System.ArgumentNullException("context");
            }
            global::System.Web.SessionState.HttpSessionStateContainer hssc = container as global::System.Web.SessionState.HttpSessionStateContainer;
            if (hssc != null)
            {
                context.SetSessionStateInstance(new global::System.Web.SessionState.HttpSessionState(hssc));
            }
        }
        public static void RemoveHttpSessionStateFromContext(global::System.Web.HttpContext context)
        {
            if (context == null)
            {
                throw new global::System.ArgumentNullException("context");
            }
            context.SetSessionStateInstance(null);
        }
        public static global::System.Web.SessionState.IHttpSessionState GetHttpSessionStateFromContext(global::System.Web.HttpContext context)
        {
            if (context == null)
            {
                throw new global::System.ArgumentNullException("context");
            }
            global::System.Web.SessionState.HttpSessionState s = context.Session;
            if (s == null)
            {
                return null;
            }
            return s.Container;
        }
        public static global::System.Web.HttpStaticObjectsCollection GetSessionStaticObjects(global::System.Web.HttpContext context)
        {
            if (context == null)
            {
                throw new global::System.ArgumentNullException("context");
            }
            global::System.Web.SessionState.HttpSessionState s = context.Session;
            if (s != null)
            {
                return s.StaticObjects;
            }
            return new global::System.Web.HttpStaticObjectsCollection();
        }
        public static global::System.Boolean IsSessionStateRequired(global::System.Web.HttpContext context)
        {
            if (context == null || context.Handler == null)
            {
                return false;
            }
            return (context.Handler is global::System.Web.SessionState.IRequiresSessionState)
                || (context.Handler is global::System.Web.SessionState.IReadOnlySessionState);
        }
        public static global::System.Boolean IsSessionStateReadOnly(global::System.Web.HttpContext context)
        {
            if (context == null || context.Handler == null)
            {
                return false;
            }
            return context.Handler is global::System.Web.SessionState.IReadOnlySessionState;
        }
        public static global::System.Runtime.Serialization.ISurrogateSelector SerializationSurrogateSelector { get; set; }
    }
    public interface IStateRuntime
    {
        void StopProcessing();
        void ProcessRequest(global::System.IntPtr tracker, global::System.Int32 verb, global::System.String uri, global::System.Int32 exclusive, global::System.Int32 timeout, global::System.Int32 lockCookieExists, global::System.Int32 lockCookie, global::System.Int32 contentLength, global::System.IntPtr content);
        void ProcessRequest(global::System.IntPtr tracker, global::System.Int32 verb, global::System.String uri, global::System.Int32 exclusive, global::System.Int32 extraFlags, global::System.Int32 timeout, global::System.Int32 lockCookieExists, global::System.Int32 lockCookie, global::System.Int32 contentLength, global::System.IntPtr content);
    }
    public sealed class StateRuntime : global::System.Web.SessionState.IStateRuntime
    {
        // Out-of-proc state server runtime is not part of the Tier-3 InProc target.
        public StateRuntime() { }
        public void StopProcessing() { throw new global::System.NotImplementedException("TODO: StateServer runtime is out of scope for the InProc Tier-3 target."); }
        public void ProcessRequest(global::System.IntPtr tracker, global::System.Int32 verb, global::System.String uri, global::System.Int32 exclusive, global::System.Int32 timeout, global::System.Int32 lockCookieExists, global::System.Int32 lockCookie, global::System.Int32 contentLength, global::System.IntPtr content) { throw new global::System.NotImplementedException("TODO: StateServer runtime is out of scope for the InProc Tier-3 target."); }
        public void ProcessRequest(global::System.IntPtr tracker, global::System.Int32 verb, global::System.String uri, global::System.Int32 exclusive, global::System.Int32 extraFlags, global::System.Int32 timeout, global::System.Int32 lockCookieExists, global::System.Int32 lockCookie, global::System.Int32 contentLength, global::System.IntPtr content) { throw new global::System.NotImplementedException("TODO: StateServer runtime is out of scope for the InProc Tier-3 target."); }
    }
}
