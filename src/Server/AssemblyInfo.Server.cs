// Expose the internal standalone server types to the sample host and the test project.
// The assembly is unsigned, so simple-name InternalsVisibleTo is sufficient.
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SampleHost")]
[assembly: InternalsVisibleTo("System.Web.Tests")]
