﻿using DuetAPI.ObjectModel;
using DuetAPI.Utility;
using System;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DuetControlServer.Commands
{
    /// <summary>
    /// Implementation of the <see cref="DuetAPI.Commands.StopPlugin"/> command
    /// </summary>
    public sealed class StopPlugin : DuetAPI.Commands.StopPlugin
    {
        /// <summary>
        /// Stop a plugin
        /// </summary>
        /// <returns>Asynchronous task</returns>
        /// <exception cref="ArgumentException">Plugin is invalid</exception>
        public override async Task Execute()
        {
            if (!Settings.PluginSupport)
            {
                throw new NotSupportedException("Plugin support has been disabled");
            }

            bool stopPlugin = false, asRoot = false;
            using (await Model.Provider.AccessReadWriteAsync())
            {
                if (Model.Provider.Get.Plugins.TryGetValue(Plugin, out Plugin plugin))
                {
                    if (plugin.Pid > 0)
                    {
                        // Make sure no other running plugin depends on it
                        if (!StoppingAll)
                        {
                            foreach (Plugin other in Model.Provider.Get.Plugins.Values)
                            {
                                if (other.Id != Plugin && other.Pid > 0 && other.SbcPluginDependencies.Contains(Plugin))
                                {
                                    throw new ArgumentException($"Cannot stop plugin because plugin {other.Id} depends on it");
                                }
                            }
                        }

                        // Stop the plugin
                        plugin.Pid = 0;
                        stopPlugin = true;
                        asRoot = plugin.SbcPermissions.HasFlag(SbcPermissions.SuperUser);
                    }
                }
                else
                {
                    throw new ArgumentException($"Plugin {Plugin} not found");
                }
            }

            if (stopPlugin)
            {
                // Stop it via the plugin service. This will reset the PID to -1 too
                await IPC.Processors.PluginService.PerformCommand(this, asRoot);
            }

            // Save the execution state if requested
            if (SaveState)
            {
                await using FileStream fileStream = new(Settings.PluginsFilename, FileMode.Create, FileAccess.Write);
                await using StreamWriter writer = new(fileStream);
                using (await Model.Provider.AccessReadOnlyAsync())
                {
                    foreach (Plugin item in Model.Provider.Get.Plugins.Values)
                    {
                        if (item.Pid >= 0 && item.Id != Plugin)
                        {
                            await writer.WriteLineAsync(item.Id);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// This is set to true if all the plugins are supposed to be stopped
        /// </summary>
        [JsonIgnore]
        public bool StoppingAll { get; set; }
    }
}
