using System.Collections.Generic;

namespace CustomShell
{
    public class ShellConfig
    {
        public bool GroupTaskbarWindows { get; set; } = false;
        public string Theme { get; set; } = "Windows11";
        public List<ShortcutItem> Shortcuts { get; set; } = new List<ShortcutItem>();
    }
}
