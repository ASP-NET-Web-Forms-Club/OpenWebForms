using Xunit;

// ---------------------------------------------------------------------------
// Test-collection parallelization policy.
//
// The whole suite drives OUR clean-room System.Web through a single shared
// AssemblyLoadContext (SystemWebUnderTest). Inside that ALC, HttpContext.Current
// is a process-wide thread-static, and Page.Context / Page.Items fall back to it
// whenever a hand-built Page has no explicit context (which is every worker page).
//
// xUnit, by default, runs different test *collections* (one per test class) in
// PARALLEL on the thread pool. SiteMapWorker legitimately assigns
// HttpContext.Current = ctx for the duration of a SiteMap test (the
// SiteMapProvider resolves CurrentNode from it). While that assignment is live,
// a concurrently-running test on another pool thread that reads the ambient
// context (e.g. the WebParts pages, which register their manager during the
// init pass) can observe SiteMap's transient context. The two collections then
// interleave over the same thread-static and the WebParts init intermittently
// trips "Only one WebPartManager per page." on a fresh rebuild.
//
// Because the leaking state is a single process-wide thread-static that is read
// by virtually every page-driving test class (~27 of them), a per-class
// [Collection] grouping would have to enrol almost the entire suite to be sound
// and would be fragile (any new page test silently outside the group would
// re-introduce the race). Serializing the assembly removes the shared-thread-
// static interleaving deterministically with no per-class bookkeeping. The full
// suite runs in ~2s, so the cost is negligible and the determinism is total.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
