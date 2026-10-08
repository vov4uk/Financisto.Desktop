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

        /// <summary>
        /// Assemblies the plugin shares with the app: the contract (so <c>IBankHelper</c> is the same type on both sides) and the logger.
        /// Keep in step with <c>HostProvidedAssembly</c> in src/BankHelpers/Plugins/Directory.Build.targets.
        /// </summary>
        private static readonly HashSet<string> HostProvidedAssemblies = new(StringComparer.OrdinalIgnoreCase)
        {
            "Financisto.BankHelpers.Abstractions",
            "NLog",
        };

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
    }
}
