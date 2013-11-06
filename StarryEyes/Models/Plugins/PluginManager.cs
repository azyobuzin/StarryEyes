using StarryEyes.Feather;
using StarryEyes.Feather.Scripting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace StarryEyes.Models.Plugins
{
    public static class PluginManager
    {
        private static Exception _thrownException;
        internal static Exception ThrownException
        {
            get { return _thrownException; }
        }

        private static readonly object _pluginsLocker = new object();
        private static readonly List<IPlugin> _plugins = new List<IPlugin>();

        public static IEnumerable<IPlugin> LoadedPlugins
        {
            get
            {
                lock (_pluginsLocker)
                {
                    return _plugins.AsReadOnly();
                }
            }
        }

        internal static void Load(string path)
        {
            try
            {
                Directory.CreateDirectory(path).EnumerateFiles("*.dll")
                    .ForEach(file =>
                    {
                        Assembly.LoadFrom(file.FullName)
                            .GetTypes()
                            .Where(type => typeof(IPlugin).IsAssignableFrom(type))
                            .Select(type => Activator.CreateInstance(type) as IPlugin)
                            .Do(instance => _plugins.Add(instance))
                            .OfType<StarryEyes.Feather.ConcreteInterfaces.IScriptExecutor>()
                            .ForEach(instance => instance.Extensions.ForEach(ext =>
                                ScriptingManager.RegisterExecutor(ext, new PluginScriptExecutor(instance))));
                    });
            }
            catch (Exception ex)
            {
                _thrownException = ex;
            }
        }

        private class PluginScriptExecutor : StarryEyes.Feather.Scripting.IScriptExecutor
        {
            public PluginScriptExecutor(StarryEyes.Feather.ConcreteInterfaces.IScriptExecutor plugin)
            {
                _plugin = plugin;
            }

            StarryEyes.Feather.ConcreteInterfaces.IScriptExecutor _plugin;

            public void ExecuteScript(string filePath)
            {
                _plugin.Execute(File.ReadAllText(filePath));
            }
        }
    }
}
