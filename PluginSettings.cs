using System.IO;
using NbCore.Plugins;
using Newtonsoft.Json;
using ImGuiNET;
using System.Reflection;

namespace NibbleZwiftPlugin
{
    public class ZwiftPluginSettings : PluginSettings
    {
        [JsonIgnore]
        public static string DefaultSettingsFileName = "NbPlugin_Zwift.ini";
        public string BaseDir;

        public new static PluginSettings GenerateDefaultSettings()
        {
            ZwiftPluginSettings settings = new()
            {
                BaseDir = ""
            };
            return settings;
        }

        public override void Draw()
        {
            ImGui.InputText("Base Directory", ref BaseDir, 200);
        }

        public override void DrawModals()
        {

        }

        public override void SaveToFile()
        {
            string jsondata = JsonConvert.SerializeObject(this);
            //Get Plugin Directory
            string plugindirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            File.WriteAllText(Path.Join(plugindirectory, DefaultSettingsFileName), jsondata);
        }
    }
}
