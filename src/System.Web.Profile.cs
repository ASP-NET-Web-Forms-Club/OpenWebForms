// Clean-room System.Web.Profile implementation (net8.0).
// Default in-memory profile provider + ProfileBase/ProfileModule.
// No LINQ; explicit for/foreach only. Built from public signatures and
// documented ASP.NET profile behavior (MSDN) and standard library contracts.
#nullable disable
#pragma warning disable

namespace System.Web.Profile
{
    public sealed class ProfileProviderAttribute : global::System.Attribute
    {
        private readonly global::System.String _providerName;
        public ProfileProviderAttribute(global::System.String providerName)
        {
            if (global::System.String.IsNullOrEmpty(providerName)) { throw new global::System.ArgumentNullException("providerName"); }
            _providerName = providerName;
        }
        public global::System.String ProviderName { get { return _providerName; } }
    }

    public sealed class SettingsAllowAnonymousAttribute : global::System.Attribute
    {
        private readonly global::System.Boolean _allow;
        public SettingsAllowAnonymousAttribute(global::System.Boolean allow) { _allow = allow; }
        public override global::System.Boolean IsDefaultAttribute() { return !_allow; }
        public global::System.Boolean Allow { get { return _allow; } }
    }

    public sealed class CustomProviderDataAttribute : global::System.Attribute
    {
        private readonly global::System.String _data;
        public CustomProviderDataAttribute(global::System.String customProviderData) { _data = customProviderData; }
        public override global::System.Boolean IsDefaultAttribute() { return global::System.String.IsNullOrEmpty(_data); }
        public global::System.String CustomProviderData { get { return _data; } }
    }

    public class DefaultProfile : global::System.Web.Profile.ProfileBase
    {
        public DefaultProfile() : base() { }
    }

    public class ProfileBase : global::System.Configuration.SettingsBase
    {
        // The single shared SettingsPropertyCollection that describes the
        // dynamically-discovered profile properties. In a real config-driven
        // implementation this is built from the <profile><properties> section;
        // here it is an empty, lazily-created collection that callers (or the
        // dynamic-property API) may populate.
        private static global::System.Configuration.SettingsPropertyCollection s_properties;
        private static readonly global::System.Object s_propertiesLock = new global::System.Object();

        private global::System.String _userName;
        private global::System.Boolean _isAnonymous;
        private global::System.Boolean _initialized;
        private global::System.Boolean _dirty;
        private global::System.DateTime _lastActivityDate;
        private global::System.DateTime _lastUpdatedDate;

        // Backing store for property values for this profile instance.
        private readonly global::System.Collections.Generic.Dictionary<global::System.String, global::System.Object> _values
            = new global::System.Collections.Generic.Dictionary<global::System.String, global::System.Object>(global::System.StringComparer.Ordinal);
        // Tracks whether persisted values were merged from the provider yet.
        private global::System.Boolean _valuesLoaded;
        private readonly global::System.Object _instanceLock = new global::System.Object();

        // Cached profile group instances by group name.
        private global::System.Collections.Generic.Dictionary<global::System.String, global::System.Web.Profile.ProfileGroupBase> _groups;

        public ProfileBase() : base() { }

        internal global::System.Boolean Initialized { get { return _initialized; } }

        public void Initialize(global::System.String username, global::System.Boolean isAuthenticated)
        {
            _userName = username;
            _isAnonymous = !isAuthenticated;
            _lastActivityDate = global::System.DateTime.UtcNow;
            _lastUpdatedDate = global::System.DateTime.UtcNow;
            _initialized = true;
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
            {
                throw new global::System.InvalidOperationException("ProfileBase has not been initialized. Call Initialize first.");
            }
        }

        // Loads any persisted values for this user from the default provider on
        // first access. We treat the provider as the source of truth and merge
        // its results into the in-memory dictionary.
        private void EnsureValuesLoaded()
        {
            if (_valuesLoaded) { return; }
            lock (_instanceLock)
            {
                if (_valuesLoaded) { return; }
                _valuesLoaded = true;
                global::System.Web.Profile.ProfileProvider provider = global::System.Web.Profile.ProfileManager.Provider;
                if (provider != null && _userName != null)
                {
                    global::System.Collections.Generic.IDictionary<global::System.String, global::System.Object> loaded
                        = global::System.Web.Profile.DefaultProfileProvider.TryGetRawValues(provider, _userName);
                    if (loaded != null)
                    {
                        foreach (global::System.Collections.Generic.KeyValuePair<global::System.String, global::System.Object> kv in loaded)
                        {
                            if (!_values.ContainsKey(kv.Key))
                            {
                                _values[kv.Key] = kv.Value;
                            }
                        }
                    }
                }
            }
        }

        public global::System.Object GetPropertyValue(global::System.String propertyName)
        {
            if (propertyName == null) { throw new global::System.ArgumentNullException("propertyName"); }
            EnsureInitialized();
            EnsureValuesLoaded();
            lock (_instanceLock)
            {
                _lastActivityDate = global::System.DateTime.UtcNow;
                global::System.Object v;
                if (_values.TryGetValue(propertyName, out v)) { return v; }
                return null;
            }
        }

        public void SetPropertyValue(global::System.String propertyName, global::System.Object propertyValue)
        {
            if (propertyName == null) { throw new global::System.ArgumentNullException("propertyName"); }
            EnsureInitialized();
            EnsureValuesLoaded();
            lock (_instanceLock)
            {
                _values[propertyName] = propertyValue;
                _dirty = true;
                _lastActivityDate = global::System.DateTime.UtcNow;
            }
        }

        public global::System.Web.Profile.ProfileGroupBase GetProfileGroup(global::System.String groupName)
        {
            if (groupName == null) { throw new global::System.ArgumentNullException("groupName"); }
            lock (_instanceLock)
            {
                if (_groups == null)
                {
                    _groups = new global::System.Collections.Generic.Dictionary<global::System.String, global::System.Web.Profile.ProfileGroupBase>(global::System.StringComparer.Ordinal);
                }
                global::System.Web.Profile.ProfileGroupBase group;
                if (!_groups.TryGetValue(groupName, out group))
                {
                    group = new global::System.Web.Profile.ProfileGroupBase();
                    group.Init(this, groupName);
                    _groups[groupName] = group;
                }
                return group;
            }
        }

        // Group properties are stored in the parent under "<group>.<property>".
        internal global::System.Object GetGroupPropertyValue(global::System.String groupName, global::System.String propertyName)
        {
            return GetPropertyValue(groupName + "." + propertyName);
        }

        internal void SetGroupPropertyValue(global::System.String groupName, global::System.String propertyName, global::System.Object value)
        {
            SetPropertyValue(groupName + "." + propertyName, value);
        }

        public override void Save()
        {
            EnsureInitialized();
            global::System.Web.Profile.ProfileProvider provider = global::System.Web.Profile.ProfileManager.Provider;
            global::System.Collections.Generic.Dictionary<global::System.String, global::System.Object> snapshot;
            lock (_instanceLock)
            {
                if (!_dirty) { return; }
                snapshot = new global::System.Collections.Generic.Dictionary<global::System.String, global::System.Object>(_values, global::System.StringComparer.Ordinal);
            }
            if (provider != null && _userName != null)
            {
                global::System.Web.Profile.DefaultProfileProvider.SaveRawValues(provider, _userName, _isAnonymous, snapshot);
            }
            lock (_instanceLock)
            {
                _dirty = false;
                _lastUpdatedDate = global::System.DateTime.UtcNow;
                _lastActivityDate = _lastUpdatedDate;
            }
        }

        public static global::System.Web.Profile.ProfileBase Create(global::System.String username)
        {
            return Create(username, true);
        }

        public static global::System.Web.Profile.ProfileBase Create(global::System.String username, global::System.Boolean isAuthenticated)
        {
            global::System.Web.Profile.ProfileBase profile = new global::System.Web.Profile.DefaultProfile();
            profile.Initialize(username, isAuthenticated);
            return profile;
        }

        public override global::System.Object this[global::System.String propertyName]
        {
            get { return GetPropertyValue(propertyName); }
            set { SetPropertyValue(propertyName, value); }
        }

        public global::System.String UserName { get { return _userName; } }

        public global::System.Boolean IsAnonymous { get { return _isAnonymous; } }

        public global::System.Boolean IsDirty
        {
            get { lock (_instanceLock) { return _dirty; } }
        }

        public global::System.DateTime LastActivityDate
        {
            get { return _lastActivityDate; }
        }

        public global::System.DateTime LastUpdatedDate
        {
            get { return _lastUpdatedDate; }
        }

        public static global::System.Configuration.SettingsPropertyCollection Properties
        {
            get
            {
                if (s_properties == null)
                {
                    lock (s_propertiesLock)
                    {
                        if (s_properties == null)
                        {
                            s_properties = new global::System.Configuration.SettingsPropertyCollection();
                        }
                    }
                }
                return s_properties;
            }
        }

        // Allows the dynamic-property API (ProfileManager.AddDynamicProfileProperty)
        // to register an additional profile property at runtime.
        internal static void AddDynamicProperty(global::System.Configuration.SettingsProperty property)
        {
            if (property == null) { return; }
            global::System.Configuration.SettingsPropertyCollection props = Properties;
            lock (s_propertiesLock)
            {
                if (props[property.Name] == null)
                {
                    // Add throws NotSupportedException if the collection has
                    // been made read-only; swallow that to keep registration safe.
                    try { props.Add(property); }
                    catch (global::System.NotSupportedException) { }
                }
            }
        }
    }

    public class ProfileGroupBase
    {
        private global::System.Web.Profile.ProfileBase _parent;
        private global::System.String _name;

        public ProfileGroupBase() { }

        public global::System.Object GetPropertyValue(global::System.String propertyName)
        {
            if (_parent == null) { throw new global::System.InvalidOperationException("ProfileGroupBase has not been initialized."); }
            return _parent.GetGroupPropertyValue(_name, propertyName);
        }

        public void SetPropertyValue(global::System.String propertyName, global::System.Object propertyValue)
        {
            if (_parent == null) { throw new global::System.InvalidOperationException("ProfileGroupBase has not been initialized."); }
            _parent.SetGroupPropertyValue(_name, propertyName, propertyValue);
        }

        public void Init(global::System.Web.Profile.ProfileBase parent, global::System.String myName)
        {
            // Only assign once; subsequent calls are ignored, matching the
            // documented "initialize a profile group" semantics.
            if (_parent == null)
            {
                _parent = parent;
                _name = myName;
            }
        }

        public global::System.Object this[global::System.String propertyName]
        {
            get { return GetPropertyValue(propertyName); }
            set { SetPropertyValue(propertyName, value); }
        }
    }

    public enum ProfileAuthenticationOption
    {
        Anonymous = 0,
        Authenticated = 1,
        All = 2,
    }

    public sealed class ProfileEventArgs : global::System.EventArgs
    {
        private readonly global::System.Web.HttpContext _context;
        private global::System.Web.Profile.ProfileBase _profile;
        public ProfileEventArgs(global::System.Web.HttpContext context) { _context = context; }
        public global::System.Web.HttpContext Context { get { return _context; } }
        public global::System.Web.Profile.ProfileBase Profile { get { return _profile; } set { _profile = value; } }
    }

    public delegate void ProfileEventHandler(global::System.Object sender, global::System.Web.Profile.ProfileEventArgs e);

    public class ProfileInfo
    {
        private global::System.String _userName;
        private global::System.Boolean _isAnonymous;
        private global::System.DateTime _lastActivityDate;
        private global::System.DateTime _lastUpdatedDate;
        private global::System.Int32 _size;

        public ProfileInfo(global::System.String username, global::System.Boolean isAnonymous, global::System.DateTime lastActivityDate, global::System.DateTime lastUpdatedDate, global::System.Int32 size)
        {
            if (username != null) { username = username.Trim(); }
            if (global::System.String.IsNullOrEmpty(username)) { throw new global::System.ArgumentException("username cannot be empty.", "username"); }
            _userName = username;
            _isAnonymous = isAnonymous;
            _lastActivityDate = lastActivityDate.ToUniversalTime();
            _lastUpdatedDate = lastUpdatedDate.ToUniversalTime();
            _size = size;
        }

        protected ProfileInfo() { }

        public virtual global::System.String UserName { get { return _userName; } }
        public virtual global::System.DateTime LastActivityDate { get { return _lastActivityDate.ToLocalTime(); } }
        public virtual global::System.DateTime LastUpdatedDate { get { return _lastUpdatedDate.ToLocalTime(); } }
        public virtual global::System.Boolean IsAnonymous { get { return _isAnonymous; } }
        public virtual global::System.Int32 Size { get { return _size; } }
    }

    public sealed class ProfileInfoCollection : global::System.Collections.IEnumerable, global::System.Collections.ICollection
    {
        private readonly global::System.Collections.Generic.Dictionary<global::System.String, global::System.Web.Profile.ProfileInfo> _map
            = new global::System.Collections.Generic.Dictionary<global::System.String, global::System.Web.Profile.ProfileInfo>(global::System.StringComparer.OrdinalIgnoreCase);
        // Maintains insertion order for enumeration / CopyTo.
        private readonly global::System.Collections.Generic.List<global::System.String> _order
            = new global::System.Collections.Generic.List<global::System.String>();
        private global::System.Boolean _readOnly;
        private readonly global::System.Object _syncRoot = new global::System.Object();

        public ProfileInfoCollection() { }

        public void Add(global::System.Web.Profile.ProfileInfo profileInfo)
        {
            if (_readOnly) { throw new global::System.NotSupportedException("Collection is read-only."); }
            if (profileInfo == null) { throw new global::System.ArgumentNullException("profileInfo"); }
            if (profileInfo.UserName == null) { throw new global::System.ArgumentNullException("profileInfo.UserName"); }
            lock (_syncRoot)
            {
                if (_map.ContainsKey(profileInfo.UserName))
                {
                    throw new global::System.ArgumentException("A ProfileInfo with the same UserName already exists.");
                }
                _map.Add(profileInfo.UserName, profileInfo);
                _order.Add(profileInfo.UserName);
            }
        }

        public void Remove(global::System.String name)
        {
            if (_readOnly) { throw new global::System.NotSupportedException("Collection is read-only."); }
            if (name == null) { throw new global::System.ArgumentNullException("name"); }
            lock (_syncRoot)
            {
                if (_map.Remove(name))
                {
                    for (global::System.Int32 i = 0; i < _order.Count; i++)
                    {
                        if (global::System.String.Equals(_order[i], name, global::System.StringComparison.OrdinalIgnoreCase))
                        {
                            _order.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
        }

        public global::System.Collections.IEnumerator GetEnumerator()
        {
            global::System.Collections.Generic.List<global::System.Web.Profile.ProfileInfo> items
                = new global::System.Collections.Generic.List<global::System.Web.Profile.ProfileInfo>(_order.Count);
            lock (_syncRoot)
            {
                for (global::System.Int32 i = 0; i < _order.Count; i++)
                {
                    items.Add(_map[_order[i]]);
                }
            }
            return items.GetEnumerator();
        }

        public void SetReadOnly()
        {
            _readOnly = true;
        }

        public void Clear()
        {
            if (_readOnly) { throw new global::System.NotSupportedException("Collection is read-only."); }
            lock (_syncRoot)
            {
                _map.Clear();
                _order.Clear();
            }
        }

        public void CopyTo(global::System.Array array, global::System.Int32 index)
        {
            if (array == null) { throw new global::System.ArgumentNullException("array"); }
            lock (_syncRoot)
            {
                global::System.Int32 i = index;
                for (global::System.Int32 j = 0; j < _order.Count; j++)
                {
                    array.SetValue(_map[_order[j]], i);
                    i++;
                }
            }
        }

        public void CopyTo(global::System.Web.Profile.ProfileInfo[] array, global::System.Int32 index)
        {
            if (array == null) { throw new global::System.ArgumentNullException("array"); }
            lock (_syncRoot)
            {
                global::System.Int32 i = index;
                for (global::System.Int32 j = 0; j < _order.Count; j++)
                {
                    array[i] = _map[_order[j]];
                    i++;
                }
            }
        }

        public global::System.Web.Profile.ProfileInfo this[global::System.String name]
        {
            get
            {
                if (name == null) { throw new global::System.ArgumentNullException("name"); }
                lock (_syncRoot)
                {
                    global::System.Web.Profile.ProfileInfo info;
                    if (_map.TryGetValue(name, out info)) { return info; }
                    return null;
                }
            }
        }

        public global::System.Int32 Count { get { lock (_syncRoot) { return _order.Count; } } }
        public global::System.Boolean IsSynchronized { get { return false; } }
        public global::System.Object SyncRoot { get { return _syncRoot; } }
    }

    public static class ProfileManager
    {
        private static global::System.String s_applicationName = "/";
        private static global::System.Web.Profile.ProfileProviderCollection s_providers;
        private static global::System.Web.Profile.ProfileProvider s_provider;
        private static readonly global::System.Object s_lock = new global::System.Object();

        private static void EnsureProviders()
        {
            if (s_providers != null) { return; }
            lock (s_lock)
            {
                if (s_providers != null) { return; }
                global::System.Web.Profile.ProfileProviderCollection providers = new global::System.Web.Profile.ProfileProviderCollection();
                global::System.Web.Profile.DefaultProfileProvider def = new global::System.Web.Profile.DefaultProfileProvider();
                global::System.Collections.Specialized.NameValueCollection config = new global::System.Collections.Specialized.NameValueCollection();
                config["applicationName"] = s_applicationName;
                def.Initialize("DefaultProfileProvider", config);
                providers.Add(def);
                providers.SetReadOnly();
                s_provider = def;
                s_providers = providers;
            }
        }

        public static void AddDynamicProfileProperty(global::System.Web.Configuration.ProfilePropertySettings property)
        {
            if (property == null) { throw new global::System.ArgumentNullException("property"); }
            // Translate the configuration entry into a SettingsProperty and
            // register it on the shared ProfileBase.Properties collection.
            global::System.Type t = typeof(global::System.String);
            global::System.Configuration.SettingsProperty sp = new global::System.Configuration.SettingsProperty(property.Name);
            sp.PropertyType = t;
            sp.IsReadOnly = false;
            global::System.Web.Profile.ProfileBase.AddDynamicProperty(sp);
        }

        public static global::System.Boolean DeleteProfile(global::System.String username)
        {
            if (username == null) { throw new global::System.ArgumentNullException("username"); }
            EnsureProviders();
            global::System.Int32 deleted = s_provider.DeleteProfiles(new global::System.String[] { username });
            return deleted > 0;
        }

        public static global::System.Int32 DeleteProfiles(global::System.Web.Profile.ProfileInfoCollection profiles)
        {
            if (profiles == null) { throw new global::System.ArgumentNullException("profiles"); }
            EnsureProviders();
            return s_provider.DeleteProfiles(profiles);
        }

        public static global::System.Int32 DeleteProfiles(global::System.String[] usernames)
        {
            if (usernames == null) { throw new global::System.ArgumentNullException("usernames"); }
            EnsureProviders();
            return s_provider.DeleteProfiles(usernames);
        }

        public static global::System.Int32 DeleteInactiveProfiles(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.DateTime userInactiveSinceDate)
        {
            EnsureProviders();
            return s_provider.DeleteInactiveProfiles(authenticationOption, userInactiveSinceDate);
        }

        public static global::System.Int32 GetNumberOfProfiles(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption)
        {
            EnsureProviders();
            global::System.Int32 total;
            s_provider.GetAllProfiles(authenticationOption, 0, global::System.Int32.MaxValue, out total);
            return total;
        }

        public static global::System.Int32 GetNumberOfInactiveProfiles(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.DateTime userInactiveSinceDate)
        {
            EnsureProviders();
            return s_provider.GetNumberOfInactiveProfiles(authenticationOption, userInactiveSinceDate);
        }

        public static global::System.Web.Profile.ProfileInfoCollection GetAllProfiles(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption)
        {
            EnsureProviders();
            global::System.Int32 total;
            return s_provider.GetAllProfiles(authenticationOption, 0, global::System.Int32.MaxValue, out total);
        }

        public static global::System.Web.Profile.ProfileInfoCollection GetAllProfiles(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.Int32 pageIndex, global::System.Int32 pageSize, out global::System.Int32 totalRecords)
        {
            EnsureProviders();
            return s_provider.GetAllProfiles(authenticationOption, pageIndex, pageSize, out totalRecords);
        }

        public static global::System.Web.Profile.ProfileInfoCollection GetAllInactiveProfiles(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.DateTime userInactiveSinceDate)
        {
            EnsureProviders();
            global::System.Int32 total;
            return s_provider.GetAllInactiveProfiles(authenticationOption, userInactiveSinceDate, 0, global::System.Int32.MaxValue, out total);
        }

        public static global::System.Web.Profile.ProfileInfoCollection GetAllInactiveProfiles(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.DateTime userInactiveSinceDate, global::System.Int32 pageIndex, global::System.Int32 pageSize, out global::System.Int32 totalRecords)
        {
            EnsureProviders();
            return s_provider.GetAllInactiveProfiles(authenticationOption, userInactiveSinceDate, pageIndex, pageSize, out totalRecords);
        }

        public static global::System.Web.Profile.ProfileInfoCollection FindProfilesByUserName(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.String usernameToMatch)
        {
            EnsureProviders();
            global::System.Int32 total;
            return s_provider.FindProfilesByUserName(authenticationOption, usernameToMatch, 0, global::System.Int32.MaxValue, out total);
        }

        public static global::System.Web.Profile.ProfileInfoCollection FindProfilesByUserName(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.String usernameToMatch, global::System.Int32 pageIndex, global::System.Int32 pageSize, out global::System.Int32 totalRecords)
        {
            EnsureProviders();
            return s_provider.FindProfilesByUserName(authenticationOption, usernameToMatch, pageIndex, pageSize, out totalRecords);
        }

        public static global::System.Web.Profile.ProfileInfoCollection FindInactiveProfilesByUserName(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.String usernameToMatch, global::System.DateTime userInactiveSinceDate)
        {
            EnsureProviders();
            global::System.Int32 total;
            return s_provider.FindInactiveProfilesByUserName(authenticationOption, usernameToMatch, userInactiveSinceDate, 0, global::System.Int32.MaxValue, out total);
        }

        public static global::System.Web.Profile.ProfileInfoCollection FindInactiveProfilesByUserName(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.String usernameToMatch, global::System.DateTime userInactiveSinceDate, global::System.Int32 pageIndex, global::System.Int32 pageSize, out global::System.Int32 totalRecords)
        {
            EnsureProviders();
            return s_provider.FindInactiveProfilesByUserName(authenticationOption, usernameToMatch, userInactiveSinceDate, pageIndex, pageSize, out totalRecords);
        }

        public static global::System.Boolean Enabled { get { return true; } }

        public static global::System.String ApplicationName
        {
            get { EnsureProviders(); return s_provider.ApplicationName; }
            set { EnsureProviders(); s_provider.ApplicationName = value; s_applicationName = value; }
        }

        public static global::System.Boolean AutomaticSaveEnabled { get { return true; } }

        public static global::System.Web.Profile.ProfileProvider Provider
        {
            get { EnsureProviders(); return s_provider; }
        }

        public static global::System.Web.Profile.ProfileProviderCollection Providers
        {
            get { EnsureProviders(); return s_providers; }
        }
    }

    public sealed class ProfileModule : global::System.Web.IHttpModule
    {
        public ProfileModule() { }

        public void Dispose() { }

        public void Init(global::System.Web.HttpApplication app)
        {
            if (app == null) { throw new global::System.ArgumentNullException("app"); }
            // Build the per-request profile when request state is acquired, and
            // auto-save it on release if dirty (matching ProfileModule lifecycle).
            app.PostAcquireRequestState += new global::System.EventHandler(this.OnEnter);
            app.EndRequest += new global::System.EventHandler(this.OnLeave);
        }

        public event global::System.Web.Profile.ProfileEventHandler Personalize;
        public event global::System.Web.Profile.ProfileMigrateEventHandler MigrateAnonymous;
        public event global::System.Web.Profile.ProfileAutoSaveEventHandler ProfileAutoSaving;

        private void OnEnter(global::System.Object sender, global::System.EventArgs e)
        {
            global::System.Web.HttpApplication app = sender as global::System.Web.HttpApplication;
            if (app == null) { return; }
            global::System.Web.HttpContext context = app.Context;
            if (context == null) { return; }

            global::System.String userName = null;
            global::System.Boolean isAuthenticated = false;
            global::System.Security.Principal.IPrincipal principal = context.User;
            if (principal != null && principal.Identity != null)
            {
                isAuthenticated = principal.Identity.IsAuthenticated;
                userName = principal.Identity.Name;
            }
            if (userName == null) { userName = global::System.String.Empty; }

            global::System.Web.Profile.ProfileBase profile = new global::System.Web.Profile.DefaultProfile();
            profile.Initialize(userName, isAuthenticated);

            // Allow a host to personalize / replace the profile instance.
            global::System.Web.Profile.ProfileEventHandler personalizeHandler = this.Personalize;
            if (personalizeHandler != null)
            {
                global::System.Web.Profile.ProfileEventArgs args = new global::System.Web.Profile.ProfileEventArgs(context);
                args.Profile = profile;
                personalizeHandler(this, args);
                if (args.Profile != null) { profile = args.Profile; }
            }

            context.SetProfileInstance(profile);
        }

        private void OnLeave(global::System.Object sender, global::System.EventArgs e)
        {
            global::System.Web.HttpApplication app = sender as global::System.Web.HttpApplication;
            if (app == null) { return; }
            global::System.Web.HttpContext context = app.Context;
            if (context == null) { return; }
            global::System.Web.Profile.ProfileBase profile = context.Profile;
            if (profile == null) { return; }

            if (!global::System.Web.Profile.ProfileManager.AutomaticSaveEnabled) { return; }
            if (!profile.IsDirty) { return; }

            // Give the host a chance to cancel the automatic save.
            global::System.Web.Profile.ProfileAutoSaveEventHandler autoSaveHandler = this.ProfileAutoSaving;
            if (autoSaveHandler != null)
            {
                global::System.Web.Profile.ProfileAutoSaveEventArgs args = new global::System.Web.Profile.ProfileAutoSaveEventArgs(context);
                autoSaveHandler(this, args);
                if (!args.ContinueWithProfileAutoSave) { return; }
            }

            profile.Save();
        }

        // Reserved for anonymous->authenticated profile migration; raised by the
        // host's anonymous-identification module when it is wired up.
        internal void OnMigrateAnonymous(global::System.Web.HttpContext context, global::System.String anonymousId)
        {
            global::System.Web.Profile.ProfileMigrateEventHandler handler = this.MigrateAnonymous;
            if (handler != null)
            {
                handler(this, new global::System.Web.Profile.ProfileMigrateEventArgs(context, anonymousId));
            }
        }
    }

    public delegate void ProfileMigrateEventHandler(global::System.Object sender, global::System.Web.Profile.ProfileMigrateEventArgs e);

    public sealed class ProfileMigrateEventArgs : global::System.EventArgs
    {
        private readonly global::System.Web.HttpContext _context;
        private readonly global::System.String _anonymousId;
        public ProfileMigrateEventArgs(global::System.Web.HttpContext context, global::System.String anonymousId)
        {
            _context = context;
            _anonymousId = anonymousId;
        }
        public global::System.Web.HttpContext Context { get { return _context; } }
        public global::System.String AnonymousID { get { return _anonymousId; } }
    }

    public delegate void ProfileAutoSaveEventHandler(global::System.Object sender, global::System.Web.Profile.ProfileAutoSaveEventArgs e);

    public sealed class ProfileAutoSaveEventArgs : global::System.EventArgs
    {
        private readonly global::System.Web.HttpContext _context;
        private global::System.Boolean _continue = true;
        public ProfileAutoSaveEventArgs(global::System.Web.HttpContext context) { _context = context; }
        public global::System.Web.HttpContext Context { get { return _context; } }
        public global::System.Boolean ContinueWithProfileAutoSave { get { return _continue; } set { _continue = value; } }
    }

    public abstract class ProfileProvider : global::System.Configuration.SettingsProvider
    {
        protected ProfileProvider() { }
        public abstract global::System.Int32 DeleteProfiles(global::System.Web.Profile.ProfileInfoCollection profiles);
        public abstract global::System.Int32 DeleteProfiles(global::System.String[] usernames);
        public abstract global::System.Int32 DeleteInactiveProfiles(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.DateTime userInactiveSinceDate);
        public abstract global::System.Int32 GetNumberOfInactiveProfiles(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.DateTime userInactiveSinceDate);
        public abstract global::System.Web.Profile.ProfileInfoCollection GetAllProfiles(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.Int32 pageIndex, global::System.Int32 pageSize, out global::System.Int32 totalRecords);
        public abstract global::System.Web.Profile.ProfileInfoCollection GetAllInactiveProfiles(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.DateTime userInactiveSinceDate, global::System.Int32 pageIndex, global::System.Int32 pageSize, out global::System.Int32 totalRecords);
        public abstract global::System.Web.Profile.ProfileInfoCollection FindProfilesByUserName(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.String usernameToMatch, global::System.Int32 pageIndex, global::System.Int32 pageSize, out global::System.Int32 totalRecords);
        public abstract global::System.Web.Profile.ProfileInfoCollection FindInactiveProfilesByUserName(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.String usernameToMatch, global::System.DateTime userInactiveSinceDate, global::System.Int32 pageIndex, global::System.Int32 pageSize, out global::System.Int32 totalRecords);
    }

    // Default, process-wide, thread-safe in-memory profile provider.
    // Stores each user's property values plus activity/update timestamps.
    internal class DefaultProfileProvider : global::System.Web.Profile.ProfileProvider
    {
        // One record per user.
        private sealed class Record
        {
            public global::System.String UserName;
            public global::System.Boolean IsAnonymous;
            public global::System.DateTime LastActivityDateUtc;
            public global::System.DateTime LastUpdatedDateUtc;
            public readonly global::System.Collections.Generic.Dictionary<global::System.String, global::System.Object> Values
                = new global::System.Collections.Generic.Dictionary<global::System.String, global::System.Object>(global::System.StringComparer.Ordinal);
        }

        // Process-wide store, keyed by username (case-insensitive).
        private static readonly global::System.Collections.Generic.Dictionary<global::System.String, Record> s_store
            = new global::System.Collections.Generic.Dictionary<global::System.String, Record>(global::System.StringComparer.OrdinalIgnoreCase);
        private static readonly global::System.Object s_storeLock = new global::System.Object();

        private global::System.String _name;
        private global::System.String _applicationName = "/";

        public DefaultProfileProvider() { }

        public override void Initialize(global::System.String name, global::System.Collections.Specialized.NameValueCollection config)
        {
            if (global::System.String.IsNullOrEmpty(name)) { name = "DefaultProfileProvider"; }
            _name = name;
            if (config != null)
            {
                global::System.String app = config["applicationName"];
                if (!global::System.String.IsNullOrEmpty(app)) { _applicationName = app; }
            }
            base.Initialize(name, config);
        }

        public override global::System.String Name { get { return _name; } }

        public override global::System.String ApplicationName
        {
            get { return _applicationName; }
            set { _applicationName = value; }
        }

        // ---- internal bridge used by ProfileBase -----------------------------

        internal static global::System.Collections.Generic.IDictionary<global::System.String, global::System.Object> TryGetRawValues(global::System.Web.Profile.ProfileProvider provider, global::System.String userName)
        {
            DefaultProfileProvider def = provider as DefaultProfileProvider;
            if (def == null) { return null; }
            return def.GetRawValues(userName);
        }

        internal static void SaveRawValues(global::System.Web.Profile.ProfileProvider provider, global::System.String userName, global::System.Boolean isAnonymous, global::System.Collections.Generic.IDictionary<global::System.String, global::System.Object> values)
        {
            DefaultProfileProvider def = provider as DefaultProfileProvider;
            if (def == null) { return; }
            def.SetRawValues(userName, isAnonymous, values);
        }

        private global::System.Collections.Generic.IDictionary<global::System.String, global::System.Object> GetRawValues(global::System.String userName)
        {
            if (global::System.String.IsNullOrEmpty(userName)) { return null; }
            lock (s_storeLock)
            {
                Record rec;
                if (!s_store.TryGetValue(userName, out rec)) { return null; }
                rec.LastActivityDateUtc = global::System.DateTime.UtcNow;
                global::System.Collections.Generic.Dictionary<global::System.String, global::System.Object> copy
                    = new global::System.Collections.Generic.Dictionary<global::System.String, global::System.Object>(rec.Values, global::System.StringComparer.Ordinal);
                return copy;
            }
        }

        private void SetRawValues(global::System.String userName, global::System.Boolean isAnonymous, global::System.Collections.Generic.IDictionary<global::System.String, global::System.Object> values)
        {
            if (global::System.String.IsNullOrEmpty(userName)) { return; }
            lock (s_storeLock)
            {
                Record rec;
                if (!s_store.TryGetValue(userName, out rec))
                {
                    rec = new Record();
                    rec.UserName = userName;
                    s_store[userName] = rec;
                }
                rec.IsAnonymous = isAnonymous;
                rec.Values.Clear();
                if (values != null)
                {
                    foreach (global::System.Collections.Generic.KeyValuePair<global::System.String, global::System.Object> kv in values)
                    {
                        rec.Values[kv.Key] = kv.Value;
                    }
                }
                global::System.DateTime now = global::System.DateTime.UtcNow;
                rec.LastActivityDateUtc = now;
                rec.LastUpdatedDateUtc = now;
            }
        }

        private static global::System.Int32 EstimateSize(Record rec)
        {
            // Rough byte estimate: sum of key+string-value lengths (chars*2).
            global::System.Int32 size = 0;
            foreach (global::System.Collections.Generic.KeyValuePair<global::System.String, global::System.Object> kv in rec.Values)
            {
                if (kv.Key != null) { size += kv.Key.Length * 2; }
                global::System.String s = kv.Value as global::System.String;
                if (s != null) { size += s.Length * 2; }
                else if (kv.Value != null) { size += 8; }
            }
            return size;
        }

        private static global::System.Boolean MatchesOption(Record rec, global::System.Web.Profile.ProfileAuthenticationOption option)
        {
            if (option == global::System.Web.Profile.ProfileAuthenticationOption.All) { return true; }
            if (option == global::System.Web.Profile.ProfileAuthenticationOption.Anonymous) { return rec.IsAnonymous; }
            return !rec.IsAnonymous; // Authenticated
        }

        private static global::System.Web.Profile.ProfileInfo ToInfo(Record rec)
        {
            return new global::System.Web.Profile.ProfileInfo(rec.UserName, rec.IsAnonymous, rec.LastActivityDateUtc, rec.LastUpdatedDateUtc, EstimateSize(rec));
        }

        // Applies paging to an in-order list of records and fills a collection.
        private static global::System.Web.Profile.ProfileInfoCollection Paginate(global::System.Collections.Generic.List<Record> matches, global::System.Int32 pageIndex, global::System.Int32 pageSize, out global::System.Int32 totalRecords)
        {
            totalRecords = matches.Count;
            global::System.Web.Profile.ProfileInfoCollection result = new global::System.Web.Profile.ProfileInfoCollection();
            if (pageSize <= 0) { return result; }
            global::System.Int64 startLong = (global::System.Int64)pageIndex * (global::System.Int64)pageSize;
            if (startLong < 0 || startLong >= matches.Count) { return result; }
            global::System.Int32 start = (global::System.Int32)startLong;
            global::System.Int32 end = start + pageSize;
            if (end > matches.Count) { end = matches.Count; }
            for (global::System.Int32 i = start; i < end; i++)
            {
                result.Add(ToInfo(matches[i]));
            }
            return result;
        }

        private static global::System.Collections.Generic.List<Record> SnapshotRecords()
        {
            global::System.Collections.Generic.List<Record> list = new global::System.Collections.Generic.List<Record>(s_store.Count);
            foreach (global::System.Collections.Generic.KeyValuePair<global::System.String, Record> kv in s_store)
            {
                list.Add(kv.Value);
            }
            return list;
        }

        // ---- SettingsProvider members ----------------------------------------

        public override global::System.Configuration.SettingsPropertyValueCollection GetPropertyValues(global::System.Configuration.SettingsContext context, global::System.Configuration.SettingsPropertyCollection collection)
        {
            global::System.Configuration.SettingsPropertyValueCollection result = new global::System.Configuration.SettingsPropertyValueCollection();
            if (collection == null) { return result; }
            global::System.String userName = GetUserName(context);
            global::System.Collections.Generic.IDictionary<global::System.String, global::System.Object> stored = null;
            if (!global::System.String.IsNullOrEmpty(userName))
            {
                stored = GetRawValues(userName);
            }
            foreach (global::System.Configuration.SettingsProperty prop in collection)
            {
                global::System.Configuration.SettingsPropertyValue value = new global::System.Configuration.SettingsPropertyValue(prop);
                if (stored != null)
                {
                    global::System.Object v;
                    if (stored.TryGetValue(prop.Name, out v))
                    {
                        value.PropertyValue = v;
                    }
                }
                value.IsDirty = false;
                result.Add(value);
            }
            return result;
        }

        public override void SetPropertyValues(global::System.Configuration.SettingsContext context, global::System.Configuration.SettingsPropertyValueCollection collection)
        {
            if (collection == null) { return; }
            global::System.String userName = GetUserName(context);
            if (global::System.String.IsNullOrEmpty(userName)) { return; }
            global::System.Boolean isAnonymous = GetIsAnonymous(context);
            global::System.Collections.Generic.Dictionary<global::System.String, global::System.Object> values
                = new global::System.Collections.Generic.Dictionary<global::System.String, global::System.Object>(global::System.StringComparer.Ordinal);
            // Preserve any existing values not present in this set.
            global::System.Collections.Generic.IDictionary<global::System.String, global::System.Object> existing = GetRawValues(userName);
            if (existing != null)
            {
                foreach (global::System.Collections.Generic.KeyValuePair<global::System.String, global::System.Object> kv in existing)
                {
                    values[kv.Key] = kv.Value;
                }
            }
            foreach (global::System.Configuration.SettingsPropertyValue value in collection)
            {
                values[value.Name] = value.PropertyValue;
            }
            SetRawValues(userName, isAnonymous, values);
        }

        private static global::System.String GetUserName(global::System.Configuration.SettingsContext context)
        {
            if (context == null) { return null; }
            global::System.Object v = context["UserName"];
            if (v != null) { return v.ToString(); }
            return null;
        }

        private static global::System.Boolean GetIsAnonymous(global::System.Configuration.SettingsContext context)
        {
            if (context == null) { return true; }
            global::System.Object v = context["IsAuthenticated"];
            if (v is global::System.Boolean) { return !((global::System.Boolean)v); }
            return false;
        }

        // ---- ProfileProvider members -----------------------------------------

        public override global::System.Int32 DeleteProfiles(global::System.Web.Profile.ProfileInfoCollection profiles)
        {
            if (profiles == null) { throw new global::System.ArgumentNullException("profiles"); }
            global::System.Collections.Generic.List<global::System.String> names = new global::System.Collections.Generic.List<global::System.String>(profiles.Count);
            global::System.Collections.IEnumerator en = profiles.GetEnumerator();
            while (en.MoveNext())
            {
                global::System.Web.Profile.ProfileInfo info = en.Current as global::System.Web.Profile.ProfileInfo;
                if (info != null && info.UserName != null) { names.Add(info.UserName); }
            }
            return DeleteProfiles(names.ToArray());
        }

        public override global::System.Int32 DeleteProfiles(global::System.String[] usernames)
        {
            if (usernames == null) { throw new global::System.ArgumentNullException("usernames"); }
            global::System.Int32 count = 0;
            lock (s_storeLock)
            {
                for (global::System.Int32 i = 0; i < usernames.Length; i++)
                {
                    global::System.String u = usernames[i];
                    if (u == null) { continue; }
                    if (s_store.Remove(u)) { count++; }
                }
            }
            return count;
        }

        public override global::System.Int32 DeleteInactiveProfiles(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.DateTime userInactiveSinceDate)
        {
            global::System.DateTime cutoffUtc = userInactiveSinceDate.ToUniversalTime();
            global::System.Int32 count = 0;
            lock (s_storeLock)
            {
                global::System.Collections.Generic.List<global::System.String> toRemove = new global::System.Collections.Generic.List<global::System.String>();
                foreach (global::System.Collections.Generic.KeyValuePair<global::System.String, Record> kv in s_store)
                {
                    Record rec = kv.Value;
                    if (MatchesOption(rec, authenticationOption) && rec.LastActivityDateUtc <= cutoffUtc)
                    {
                        toRemove.Add(kv.Key);
                    }
                }
                for (global::System.Int32 i = 0; i < toRemove.Count; i++)
                {
                    if (s_store.Remove(toRemove[i])) { count++; }
                }
            }
            return count;
        }

        public override global::System.Int32 GetNumberOfInactiveProfiles(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.DateTime userInactiveSinceDate)
        {
            global::System.DateTime cutoffUtc = userInactiveSinceDate.ToUniversalTime();
            global::System.Int32 count = 0;
            lock (s_storeLock)
            {
                foreach (global::System.Collections.Generic.KeyValuePair<global::System.String, Record> kv in s_store)
                {
                    Record rec = kv.Value;
                    if (MatchesOption(rec, authenticationOption) && rec.LastActivityDateUtc <= cutoffUtc) { count++; }
                }
            }
            return count;
        }

        public override global::System.Web.Profile.ProfileInfoCollection GetAllProfiles(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.Int32 pageIndex, global::System.Int32 pageSize, out global::System.Int32 totalRecords)
        {
            global::System.Collections.Generic.List<Record> matches = new global::System.Collections.Generic.List<Record>();
            lock (s_storeLock)
            {
                global::System.Collections.Generic.List<Record> all = SnapshotRecords();
                for (global::System.Int32 i = 0; i < all.Count; i++)
                {
                    if (MatchesOption(all[i], authenticationOption)) { matches.Add(all[i]); }
                }
            }
            return Paginate(matches, pageIndex, pageSize, out totalRecords);
        }

        public override global::System.Web.Profile.ProfileInfoCollection GetAllInactiveProfiles(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.DateTime userInactiveSinceDate, global::System.Int32 pageIndex, global::System.Int32 pageSize, out global::System.Int32 totalRecords)
        {
            global::System.DateTime cutoffUtc = userInactiveSinceDate.ToUniversalTime();
            global::System.Collections.Generic.List<Record> matches = new global::System.Collections.Generic.List<Record>();
            lock (s_storeLock)
            {
                global::System.Collections.Generic.List<Record> all = SnapshotRecords();
                for (global::System.Int32 i = 0; i < all.Count; i++)
                {
                    if (MatchesOption(all[i], authenticationOption) && all[i].LastActivityDateUtc <= cutoffUtc) { matches.Add(all[i]); }
                }
            }
            return Paginate(matches, pageIndex, pageSize, out totalRecords);
        }

        public override global::System.Web.Profile.ProfileInfoCollection FindProfilesByUserName(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.String usernameToMatch, global::System.Int32 pageIndex, global::System.Int32 pageSize, out global::System.Int32 totalRecords)
        {
            global::System.Collections.Generic.List<Record> matches = new global::System.Collections.Generic.List<Record>();
            lock (s_storeLock)
            {
                global::System.Collections.Generic.List<Record> all = SnapshotRecords();
                for (global::System.Int32 i = 0; i < all.Count; i++)
                {
                    if (MatchesOption(all[i], authenticationOption) && UserNameMatches(all[i].UserName, usernameToMatch)) { matches.Add(all[i]); }
                }
            }
            return Paginate(matches, pageIndex, pageSize, out totalRecords);
        }

        public override global::System.Web.Profile.ProfileInfoCollection FindInactiveProfilesByUserName(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.String usernameToMatch, global::System.DateTime userInactiveSinceDate, global::System.Int32 pageIndex, global::System.Int32 pageSize, out global::System.Int32 totalRecords)
        {
            global::System.DateTime cutoffUtc = userInactiveSinceDate.ToUniversalTime();
            global::System.Collections.Generic.List<Record> matches = new global::System.Collections.Generic.List<Record>();
            lock (s_storeLock)
            {
                global::System.Collections.Generic.List<Record> all = SnapshotRecords();
                for (global::System.Int32 i = 0; i < all.Count; i++)
                {
                    if (MatchesOption(all[i], authenticationOption) && all[i].LastActivityDateUtc <= cutoffUtc && UserNameMatches(all[i].UserName, usernameToMatch))
                    {
                        matches.Add(all[i]);
                    }
                }
            }
            return Paginate(matches, pageIndex, pageSize, out totalRecords);
        }

        // SQL "LIKE" style matching: a trailing '%' is treated as a prefix
        // wildcard; otherwise the match is a case-insensitive substring search,
        // which is the most forgiving documented behavior for the in-memory case.
        private static global::System.Boolean UserNameMatches(global::System.String candidate, global::System.String pattern)
        {
            if (pattern == null) { return false; }
            if (candidate == null) { candidate = global::System.String.Empty; }
            if (pattern.Length == 0) { return candidate.Length == 0; }
            if (pattern[pattern.Length - 1] == '%')
            {
                global::System.String prefix = pattern.Substring(0, pattern.Length - 1);
                return candidate.StartsWith(prefix, global::System.StringComparison.OrdinalIgnoreCase);
            }
            return candidate.IndexOf(pattern, global::System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    public sealed class ProfileProviderCollection : global::System.Configuration.SettingsProviderCollection
    {
        public ProfileProviderCollection() { }

        public override void Add(global::System.Configuration.Provider.ProviderBase provider)
        {
            if (provider == null) { throw new global::System.ArgumentNullException("provider"); }
            if (!(provider is global::System.Web.Profile.ProfileProvider))
            {
                throw new global::System.ArgumentException("Provider must derive from ProfileProvider.", "provider");
            }
            base.Add(provider);
        }

        public global::System.Web.Profile.ProfileProvider this[global::System.String name]
        {
            get { return (global::System.Web.Profile.ProfileProvider)base[name]; }
        }
    }

    // Documented stub: a SQL-backed profile provider. The clean-room build does
    // not ship a database schema, so the data-access members throw
    // NotSupportedException while the configuration surface (Initialize /
    // ApplicationName) is functional so the type can be instantiated and named.
    public class SqlProfileProvider : global::System.Web.Profile.ProfileProvider
    {
        private global::System.String _name;
        private global::System.String _applicationName = "/";

        public SqlProfileProvider() { }

        public override void Initialize(global::System.String name, global::System.Collections.Specialized.NameValueCollection config)
        {
            if (global::System.String.IsNullOrEmpty(name)) { name = "SqlProfileProvider"; }
            _name = name;
            if (config != null)
            {
                global::System.String app = config["applicationName"];
                if (!global::System.String.IsNullOrEmpty(app)) { _applicationName = app; }
            }
            base.Initialize(name, config);
        }

        public override global::System.String Name { get { return _name; } }

        private static global::System.NotSupportedException NotSupported()
        {
            return new global::System.NotSupportedException("SqlProfileProvider is a documented stub in this clean-room build; no SQL profile schema is provided.");
        }

        public override global::System.Configuration.SettingsPropertyValueCollection GetPropertyValues(global::System.Configuration.SettingsContext sc, global::System.Configuration.SettingsPropertyCollection properties) { throw NotSupported(); }
        public override void SetPropertyValues(global::System.Configuration.SettingsContext sc, global::System.Configuration.SettingsPropertyValueCollection properties) { throw NotSupported(); }
        public override global::System.Int32 DeleteProfiles(global::System.Web.Profile.ProfileInfoCollection profiles) { throw NotSupported(); }
        public override global::System.Int32 DeleteProfiles(global::System.String[] usernames) { throw NotSupported(); }
        public override global::System.Int32 DeleteInactiveProfiles(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.DateTime userInactiveSinceDate) { throw NotSupported(); }
        public override global::System.Int32 GetNumberOfInactiveProfiles(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.DateTime userInactiveSinceDate) { throw NotSupported(); }
        public override global::System.Web.Profile.ProfileInfoCollection GetAllProfiles(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.Int32 pageIndex, global::System.Int32 pageSize, out global::System.Int32 totalRecords) { throw NotSupported(); }
        public override global::System.Web.Profile.ProfileInfoCollection GetAllInactiveProfiles(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.DateTime userInactiveSinceDate, global::System.Int32 pageIndex, global::System.Int32 pageSize, out global::System.Int32 totalRecords) { throw NotSupported(); }
        public override global::System.Web.Profile.ProfileInfoCollection FindProfilesByUserName(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.String usernameToMatch, global::System.Int32 pageIndex, global::System.Int32 pageSize, out global::System.Int32 totalRecords) { throw NotSupported(); }
        public override global::System.Web.Profile.ProfileInfoCollection FindInactiveProfilesByUserName(global::System.Web.Profile.ProfileAuthenticationOption authenticationOption, global::System.String usernameToMatch, global::System.DateTime userInactiveSinceDate, global::System.Int32 pageIndex, global::System.Int32 pageSize, out global::System.Int32 totalRecords) { throw NotSupported(); }

        public override global::System.String ApplicationName
        {
            get { return _applicationName; }
            set { _applicationName = value; }
        }
    }
}
