using System.Runtime.CompilerServices;

// The test assemblies assert on internals that have no business being public API — the player loop
// install and uninstall in particular, which the engine otherwise invokes by attribute.
[assembly: InternalsVisibleTo("MyGameDevTools.FixedInput.Tests")]
[assembly: InternalsVisibleTo("MyGameDevTools.FixedInput.Editor.Tests")]
[assembly: InternalsVisibleTo("MyGameDevTools.FixedInput.Entities")]
[assembly: InternalsVisibleTo("MyGameDevTools.FixedInput.Entities.Tests")]
