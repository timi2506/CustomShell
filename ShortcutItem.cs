using System.Text.Json.Serialization;
using System.Windows.Media;

namespace CustomShell
{
    public class ShortcutItem
    {
        public string Name { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;

        [JsonIgnore]
        public ImageSource? Icon { get; set; }
    }
}
