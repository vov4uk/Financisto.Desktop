using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Financisto.Desktop.Helpers.BankHelper
{
    /// <summary>
    /// Loads one plugin DLL together with the dependencies embedded in it (resources named
    /// <c>embedded-dependencies/&lt;assembly&gt;.dll</c>, put there by the plugin build, see src/BankHelpers/Plugins/Directory.Build.targets).
    /// Each plugin gets its own context, so plugins can use different versions of the same library.
    /// </summary>
    internal sealed class PluginLoadContext : AssemblyLoadContext
    {
        internal const string EmbeddedDependencyPrefix = "embedded-dependencies/";
        private const string HostProvidedAssembliesResource = "HostProvidedAssemblies.txt";

        /// <summary>
        /// Assemblies the plugin shares with the app: the contract (so <c>IBankHelper</c> is the same type on both sides) and the logger.
        /// The list is src/BankHelpers/Plugins/HostProvidedAssemblies.txt, which the plugin build also reads to leave them out of the plugin DLL.
        /// </summary>
        private static readonly HashSet<string> HostProvidedAssemblies = ReadHostProvidedAssemblies();

        private readonly Dictionary<string, Assembly> embedded = new(StringComparer.OrdinalIgnoreCase);
        private Assembly? plugin;

        public PluginLoadContext(string pluginPath)
            : base(name: $"BankHelperPlugin:{Path.GetFileNameWithoutExtension(pluginPath)}", isCollectible: false)
        {
        }

        public Assembly LoadPlugin(string pluginPath)
        {
            plugin = LoadFromAssemblyPath(pluginPath);
            return plugin;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var name = assemblyName.Name;
            if (plugin is null || name is null || HostProvidedAssemblies.Contains(name))
            {
                return null; // the app's own context supplies it
            }

            lock (embedded)
            {
                if (embedded.TryGetValue(name, out var loaded))
                {
                    return loaded;
                }

                using var stream = plugin.GetManifestResourceStream($"{EmbeddedDependencyPrefix}{name}.dll");
                if (stream is null)
                {
                    return null;
                }

                return embedded[name] = LoadFromStream(stream);
            }
        }

        private static HashSet<string> ReadHostProvidedAssemblies()
        {
            using var stream = typeof(PluginLoadContext).Assembly.GetManifestResourceStream(HostProvidedAssembliesResource)
                ?? throw new InvalidOperationException($"The embedded resource {HostProvidedAssembliesResource} is missing");
            using var reader = new StreamReader(stream);
            var names = reader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        }
    }
}
