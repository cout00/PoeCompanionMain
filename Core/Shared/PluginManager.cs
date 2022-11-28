using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Helpers;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.PluginAutoUpdate;
using ExileCore.Shared.PluginAutoUpdate.Settings;
using JM.LinqFaster;
using MoreLinq.Extensions;
using SharpDX;
using SharpDX.Direct3D11;

namespace ExileCore.Shared {
    public class PluginManager {

        private readonly GameController _gameController;
        private readonly Graphics _graphics;
        private readonly MultiThreadManager _multiThreadManager;
        private readonly SettingsContainer _settingsContainer;

        public bool AllPluginsLoaded { get; private set; }
        public string RootDirectory { get; }
        public List<PluginWrapper> Plugins { get; private set; }

        public PluginManager(
            GameController gameController,
            Graphics graphics,
            MultiThreadManager multiThreadManager,
            SettingsContainer settingsContainer
            ) {
            _gameController = gameController;
            _graphics = graphics;
            _multiThreadManager = multiThreadManager;
            _settingsContainer = settingsContainer;

            Plugins = new List<PluginWrapper>();
            RootDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _gameController.EntityListWrapper.EntityAdded += EntityListWrapperOnEntityAdded;
            _gameController.EntityListWrapper.EntityRemoved += EntityListWrapperOnEntityRemoved;
            _gameController.EntityListWrapper.EntityAddedAny += EntityListWrapperOnEntityAddedAny;
            _gameController.EntityListWrapper.EntityIgnored += EntityListWrapperOnEntityIgnored;
            _gameController.Area.OnAreaChange += AreaOnOnAreaChange;

            Task.Run(() => LoadPlugins(gameController));
        }

        private void LoadPlugins(GameController gameController) {
            var pluginLoader = new PluginLoader(_gameController, _graphics, this);

            var pluginUpdateSettings = _settingsContainer.PluginsUpdateSettings;
            var loadPluginTasks = new List<Task<List<PluginWrapper>>>();

            loadPluginTasks.AddRange(LoadCompiledDirPlugins(pluginLoader));

            Task.WaitAll(loadPluginTasks?.ToArray());

            Plugins = loadPluginTasks
                .Where(t => t.Result != null)
                .SelectMany(t => t.Result)
                .OrderBy(x => x.Order)
                .ThenByDescending(x => x.CanBeMultiThreading)
                .ThenBy(x => x.Name)
                .ToList();

            AddPluginInfoToDevTree();

            InitialisePlugins(gameController);

            AreaOnOnAreaChange(gameController.Area.CurrentArea);
            AllPluginsLoaded = true;
        }


        private List<Task<List<PluginWrapper>>> LoadCompiledDirPlugins(PluginLoader pluginLoader) {

            var loadTasks = new List<Task<List<PluginWrapper>>>();
            loadTasks.Add(Task.Run(() => pluginLoader.Load(new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DevTree")))));
            loadTasks.Add(Task.Run(() => pluginLoader.Load(new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FollowerPlugin")))));
            return loadTasks;
        }

        private void InitialisePlugins(GameController gameController) {
            if (_gameController.Settings.CoreSettings.MultiThreadLoadPlugins) {
                //Pre init some general objects because with multi threading load they can null sometimes for some plugin
                var ingameStateIngameUi = gameController.IngameState.IngameUi;
                var ingameStateData = gameController.IngameState.Data;
                var ingameStateServerData = gameController.IngameState.ServerData;
                Parallel.ForEach(Plugins, wrapper => wrapper.Initialise(gameController));
            }
            else {
                Plugins.ForEach(wrapper => wrapper.Initialise(gameController));
            }
        }

        private void AddPluginInfoToDevTree() {
            var devTree = Plugins.FirstOrDefault(x => x.Name.Equals("DevTree"));

            if (devTree != null) {
                try {
                    var fieldInfo = devTree.Plugin.GetType().GetField("Plugins");
                    List<PluginWrapper> devTreePlugins() => Plugins;
                    fieldInfo.SetValue(devTree.Plugin, (Func<List<PluginWrapper>>)devTreePlugins);
                }
                catch (Exception e) {
                    DebugWindow.LogError(e.ToString());
                }

            }
        }

        public void CloseAllPlugins() {
            foreach (var plugin in Plugins) {
                plugin.Close();
            }
        }

        private void AreaOnOnAreaChange(AreaInstance area) {
            foreach (var plugin in Plugins) {
                if (plugin.IsEnable)
                    plugin.AreaChange(area);
            }
        }

        private void EntityListWrapperOnEntityIgnored(Entity entity) {
            foreach (var plugin in Plugins) {
                if (plugin.IsEnable)
                    plugin.EntityIgnored(entity);
            }
        }

        private void EntityListWrapperOnEntityAddedAny(Entity entity) {
            foreach (var plugin in Plugins) {
                if (plugin.IsEnable)
                    plugin.EntityAddedAny(entity);
            }
        }

        private void EntityListWrapperOnEntityAdded(Entity entity) {
            if (_gameController.Settings.CoreSettings.AddedMultiThread && _multiThreadManager.ThreadsCount > 0) {
                var listJob = new List<Job>();

                Plugins.WhereF(x => x.IsEnable).Batch(_multiThreadManager.ThreadsCount)
                    .ForEach(wrappers =>
                        listJob.Add(_multiThreadManager.AddJob(() => wrappers.ForEach(x => x.EntityAdded(entity)),
                            "Entity added")));

                _multiThreadManager.Process(this);
                SpinWait.SpinUntil(() => listJob.AllF(x => x.IsCompleted), 500);
            }
            else {
                foreach (var plugin in Plugins) {
                    if (plugin.IsEnable)
                        plugin.EntityAdded(entity);
                }
            }
        }

        private void EntityListWrapperOnEntityRemoved(Entity entity) {
            foreach (var plugin in Plugins) {
                if (plugin.IsEnable)
                    plugin.EntityRemoved(entity);
            }
        }

        private void LogError(string msg) {
            DebugWindow.LogError(msg, 5);
        }

        public void ReceivePluginEvent(string eventId, object args, IPlugin owner) {
            foreach (var pluginWrapper in Plugins) {
                if (pluginWrapper.IsEnable && pluginWrapper.Plugin != owner)
                    pluginWrapper.ReceiveEvent(eventId, args);
            }
        }
    }
}
