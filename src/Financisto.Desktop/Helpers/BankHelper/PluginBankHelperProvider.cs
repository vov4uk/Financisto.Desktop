using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Financisto.BankHelpers;

namespace Financisto.Desktop.Helpers.BankHelper
{
    /// <summary>
    /// Finds the bank helpers in the plugins folder: every DLL there that references the
    /// <c>Financisto.BankHelpers.Abstractions</c> contract is loaded, and each public class in it that implements
    /// <see cref="IBankHelper"/> (with a public parameterless constructor) becomes one entry of the Import menu.
    /// A DLL that can't be loaded is logged and skipped; it never stops the app.
    /// </summary>
    public sealed class PluginBankHelperProvider : IBankHelperProvider
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private static readonly string ContractAssemblyName = typeof(IBankHelper).Assembly.GetName().Name!;

        public PluginBankHelperProvider(string pluginsDirectory)
        {
            BankHelpers = Load(pluginsDirectory);
        }

        public IReadOnlyList<IBankHelper> BankHelpers { get; }

        private static IReadOnlyList<IBankHelper> Load(string pluginsDirectory)
        {
            var helpers = new List<IBankHelper>();
            if (!Directory.Exists(pluginsDirectory))
            {
                Logger.Info($"No bank helper plugins: the folder {pluginsDirectory} does not exist");
                return helpers;
            }

            string[] files;
            try
            {
                files = Directory.EnumerateFiles(pluginsDirectory, "*.dll").OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Error(ex, $"The plugins folder {pluginsDirectory} could not be read");
                return helpers;
            }

            // The same helper class from several DLLs (e.g. an old copy left next to a newer one) is listed once: the first DLL wins.
            var sources = new Dictionary<string, string>();
            foreach (var file in files)
            {
                try
                {
                    foreach (var helper in LoadPlugin(file))
                    {
                        var typeName = helper.GetType().FullName ?? helper.GetType().Name;
                        if (sources.TryGetValue(typeName, out var firstFile))
                        {
                            Logger.Warn($"{typeName} in {file} is already provided by {firstFile}, skipped");
                            continue;
                        }

                        sources[typeName] = file;
                        helpers.Add(helper);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Plugin {file} could not be loaded");
                }
            }

            Logger.Info($"Bank helper plugins loaded from {pluginsDirectory}: {string.Join(", ", helpers.Select(x => x.GetType().Name))}");
            return helpers;
        }

        private static List<IBankHelper> LoadPlugin(string file)
        {
            var helpers = new List<IBankHelper>();
            if (!ReferencesContract(file))
            {
                Logger.Info($"{file} is not a bank helper plugin, skipped");
                return helpers;
            }

            return CreateHelpers(new PluginLoadContext(file).LoadPlugin(file), file);
        }

        /// <summary>Creates one helper for each public, non-abstract class of the assembly that implements <see cref="IBankHelper"/> and has a public parameterless constructor.</summary>
        internal static List<IBankHelper> CreateHelpers(Assembly assembly, string source)
        {
            var helpers = new List<IBankHelper>();
            var types = assembly.GetExportedTypes()
                .Where(x => x is { IsClass: true, IsAbstract: false } && typeof(IBankHelper).IsAssignableFrom(x) && x.GetConstructor(Type.EmptyTypes) is not null);

            foreach (var type in types)
            {
                try
                {
                    var helper = (IBankHelper)Activator.CreateInstance(type)!;
                    if (string.IsNullOrWhiteSpace(helper.BankTitle))
                    {
                        Logger.Warn($"{type.FullName} in {source} has no BankTitle, skipped");
                        continue;
                    }

                    helpers.Add(helper);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"{type.FullName} in {source} could not be created");
                }
            }

            return helpers;
        }

        /// <summary>Reads only the metadata, so no code of an unrelated DLL runs.</summary>
        private static bool ReferencesContract(string file)
        {
            try
            {
                using var stream = File.OpenRead(file);
                using var peReader = new PEReader(stream);
                if (!peReader.HasMetadata)
                {
                    return false;
                }

                var reader = peReader.GetMetadataReader();
                return reader.AssemblyReferences.Any(x => reader.GetString(reader.GetAssemblyReference(x).Name) == ContractAssemblyName);
            }
            catch (BadImageFormatException)
            {
                return false; // not a managed assembly
            }
        }
    }
}
