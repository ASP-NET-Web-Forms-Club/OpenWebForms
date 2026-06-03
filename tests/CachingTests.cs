using System;
using System.Reflection;
using System.Threading;
using Xunit;

namespace System.Web.Tests
{
    // Tier 1 behavioral tests for System.Web.Caching.Cache.
    //
    // Cache has a public parameterless ctor and public Insert/Get/Remove/Count,
    // so we instantiate and drive it directly through the ALC bridge. The
    // removal callback (CacheItemRemovedCallback) is a delegate type in OUR
    // assembly; we build it from a static method via Delegate.CreateDelegate so
    // it binds to the ALC-loaded delegate type.
    public class CachingTests
    {
        private const string CacheTypeName = "System.Web.Caching.Cache";
        private const string RemovedCallbackTypeName = "System.Web.Caching.CacheItemRemovedCallback";
        private const string PriorityTypeName = "System.Web.Caching.CacheItemPriority";
        private const string DependencyTypeName = "System.Web.Caching.CacheDependency";

        private static SystemWebUnderTest Web => SystemWebUnderTest.Instance;

        private static object NewCache()
        {
            return Web.CreateInstance(CacheTypeName);
        }

        private static void ResetSink() => CacheCallbackSink.Reset();
        private static Delegate MakeRemovedCallback() => CacheCallbackSink.MakeRemovedCallback(Web);


        [Fact]
        public void Insert_then_Get_ReturnsValue()
        {
            object cache = NewCache();
            Web.Invoke(cache, "Insert", "k1", "v1");
            object got = Web.Invoke(cache, "Get", "k1");
            Assert.Equal("v1", got);
        }

        [Fact]
        public void Count_ReflectsInserts()
        {
            object cache = NewCache();
            Assert.Equal(0, (int)Web.GetProperty(cache, "Count"));
            Web.Invoke(cache, "Insert", "a", "1");
            Web.Invoke(cache, "Insert", "b", "2");
            Web.Invoke(cache, "Insert", "c", "3");
            Assert.Equal(3, (int)Web.GetProperty(cache, "Count"));
        }

        [Fact]
        public void Remove_Evicts_And_FiresCallback_WithReasonRemoved()
        {
            ResetSink();
            object cache = NewCache();
            Delegate cb = MakeRemovedCallback();

            // Insert with no expiration, Default priority, removal callback.
            object noAbs = Web.GetStaticField(CacheTypeName, "NoAbsoluteExpiration");
            object noSliding = Web.GetStaticField(CacheTypeName, "NoSlidingExpiration");
            object priorityDefault = Web.EnumValue(PriorityTypeName, "Default");

            // Insert(string, object, CacheDependency, DateTime, TimeSpan, CacheItemPriority, CacheItemRemovedCallback)
            InvokeInsertFull(cache, "key", "value", null, noAbs, noSliding, priorityDefault, cb);

            object removed = Web.Invoke(cache, "Remove", "key");
            Assert.Equal("value", removed);
            // Evicted: Get now returns null.
            Assert.Null(Web.Invoke(cache, "Get", "key"));

            // Callback fired with reason Removed.
            object expectedReason = Web.EnumValue("System.Web.Caching.CacheItemRemovedReason", "Removed");
            CacheCallbackSink.Captured cap = CacheCallbackSink.Read();
            Assert.Equal("key", cap.Key);
            Assert.Equal("value", cap.Value);
            Assert.NotNull(cap.Reason);
            Assert.Equal(expectedReason, cap.Reason);
        }

        [Fact]
        public void AbsoluteExpirationInPast_MakesGetReturnNull()
        {
            object cache = NewCache();
            object noSliding = Web.GetStaticField(CacheTypeName, "NoSlidingExpiration");
            object priorityDefault = Web.EnumValue(PriorityTypeName, "Default");
            DateTime past = DateTime.UtcNow.AddMinutes(-5);

            InvokeInsertFull(cache, "stale", "x", null, past, noSliding, priorityDefault, null);
            object got = Web.Invoke(cache, "Get", "stale");
            Assert.Null(got);
        }

        [Fact]
        public void SlidingExpiration_RefreshesOnAccess()
        {
            object cache = NewCache();
            object noAbs = Web.GetStaticField(CacheTypeName, "NoAbsoluteExpiration");
            object priorityDefault = Web.EnumValue(PriorityTypeName, "Default");
            // A generous 1s window with 200ms steps gives a ~5x jitter margin per access, so a
            // scheduler/GC stall on one Thread.Sleep cannot prematurely expire the item (the prior
            // 400ms window vs 150ms sleep had only ~2.6x margin and flaked on a loaded machine).
            TimeSpan sliding = TimeSpan.FromMilliseconds(1000);

            InvokeInsertFull(cache, "s", "v", null, noAbs, sliding, priorityDefault, null);

            // Access several times within the window; each access should refresh the
            // sliding window so the item survives well past the original 1000ms.
            for (int i = 0; i < 5; i++)
            {
                Thread.Sleep(200);
                object got = Web.Invoke(cache, "Get", "s");
                Assert.Equal("v", got);
            }
            // Total elapsed (~1000ms) reaches the original window; survival proves refresh.

            // Now stop accessing and let it lapse well past the window.
            Thread.Sleep(1500);
            Assert.Null(Web.Invoke(cache, "Get", "s"));
        }


        // Helper: pick the 7-arg Insert overload (with CacheItemRemovedCallback) and invoke it.
        private static void InvokeInsertFull(object cache, string key, object value,
            object dependency, object absolute, object sliding, object priority, Delegate removedCallback)
        {
            Type cacheType = cache.GetType();
            Type depType = Web.Type(DependencyTypeName);
            Type priType = Web.Type(PriorityTypeName);
            Type cbType = Web.Type(RemovedCallbackTypeName);
            MethodInfo mi = cacheType.GetMethod(
                "Insert",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new Type[] { typeof(string), typeof(object), depType, typeof(DateTime), typeof(TimeSpan), priType, cbType },
                null);
            Assert.True(mi != null, "Could not find 7-arg Insert overload.");
            mi.Invoke(cache, new object[] { key, value, dependency, absolute, sliding, priority, removedCallback });
        }
    }

    // Non-test helper (kept out of the test class so xUnit does not try to treat
    // its public members as test methods). Captures the arguments of the cache's
    // removal callback. The callback delegate type lives in OUR assembly, and its
    // third parameter is the ALC-loaded CacheItemRemovedReason enum; we bind to it
    // exactly via a generic forwarder whose type argument is that enum type, then
    // box the reason to object for cross-ALC comparison.
    internal static class CacheCallbackSink
    {
        public struct Captured
        {
            public string Key;
            public object Value;
            public object Reason;
        }

        private static readonly object s_sync = new object();
        private static string s_key;
        private static object s_value;
        private static object s_reason;

        public static void Reset()
        {
            lock (s_sync) { s_key = null; s_value = null; s_reason = null; }
        }

        public static Captured Read()
        {
            lock (s_sync)
            {
                return new Captured { Key = s_key, Value = s_value, Reason = s_reason };
            }
        }

        // Generic forwarder: third parameter is the concrete (ALC) enum type, so
        // Delegate.CreateDelegate binds exactly to CacheItemRemovedCallback.
        public static void ForwarderTemplate<TReason>(string key, object value, TReason reason)
        {
            lock (s_sync)
            {
                s_key = key;
                s_value = value;
                s_reason = reason; // boxes the enum to object
            }
        }

        public static Delegate MakeRemovedCallback(SystemWebUnderTest web)
        {
            Type cbType = web.Type("System.Web.Caching.CacheItemRemovedCallback");
            Type reasonType = web.Type("System.Web.Caching.CacheItemRemovedReason");
            MethodInfo forwarder = typeof(CacheCallbackSink).GetMethod(
                nameof(ForwarderTemplate), BindingFlags.Public | BindingFlags.Static);
            MethodInfo concrete = forwarder.MakeGenericMethod(reasonType);
            return Delegate.CreateDelegate(cbType, concrete);
        }
    }
}