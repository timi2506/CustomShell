using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CustomShell
{
    public static class ConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public static ShellConfig LoadConfig()
        {
            if (!File.Exists(ConfigPath))
            {
                return new ShellConfig { Shortcuts = GetDefaultShortcuts() };
            }

            try
            {
                var json = File.ReadAllText(ConfigPath);
                
                try 
                {
                    var config = JsonSerializer.Deserialize<ShellConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (config != null && config.Shortcuts != null)
                        return config;
                }
                catch
                {
                    // Fallback for old List<ShortcutItem> array format
                    var items = JsonSerializer.Deserialize<List<ShortcutItem>>(json);
                    if (items != null)
                        return new ShellConfig { Shortcuts = items };
                }
                return new ShellConfig { Shortcuts = GetDefaultShortcuts() };
            }
            catch
            {
                return new ShellConfig { Shortcuts = GetDefaultShortcuts() };
            }
        }

        public static void SaveConfig(ShellConfig config)
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }

        public static List<ShortcutItem> LoadShortcuts() => LoadConfig().Shortcuts;
        
        public static void SaveShortcuts(IEnumerable<ShortcutItem> shortcuts)
        {
            var config = LoadConfig();
            config.Shortcuts = new List<ShortcutItem>(shortcuts);
            SaveConfig(config);
        }

        private static List<ShortcutItem> GetDefaultShortcuts()
        {
            var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            
            return new List<ShortcutItem>
            {
                new ShortcutItem { Name = "Steam", FilePath = Path.Combine(programFilesX86, @"Steam\steam.exe"), Arguments = "" },
                new ShortcutItem { Name = "Among Us", FilePath = Path.Combine(programFilesX86, @"Steam\steam.exe"), Arguments = "-applaunch 945360", IconPath = Path.Combine(programFilesX86, @"Steam\steamapps\common\Among Us\Among Us.exe") },
                new ShortcutItem { Name = "Task Manager", FilePath = "taskmgr.exe", Arguments = "", IconPath = Path.Combine(windir, @"System32\taskmgr.exe") },
                new ShortcutItem { Name = "Microsoft Edge", FilePath = "msedge.exe", Arguments = "", IconPath = Path.Combine(programFilesX86, @"Microsoft\Edge\Application\msedge.exe") }
            };
        }
    }
}
