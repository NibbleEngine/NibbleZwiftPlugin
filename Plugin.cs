using System;
using System.IO;
using System.Reflection;
using System.Threading;
using NbCore;
using NbCore.Plugins;
using NbCore.UI.ImGui;
using Newtonsoft.Json;
using ImGuiCore = ImGuiNET.ImGui;

namespace NibbleZwiftPlugin
{
    public class Plugin : PluginBase
    {
        private static readonly string PluginName = "ZwiftPlugin";
        private static readonly string PluginVersion = "1.0.0";
        private static readonly string PluginDescription = "Zwift Plugin for Nibble Engine. Created by gregkwaste";
        private static readonly string PluginCreator = "gregkwaste";
        
        private OpenFileDialog openFileDialog;
        
        public Plugin(Engine e) : base(e)
        {
            Name = PluginName;
            Version = PluginVersion;
            Description = PluginDescription;
            Creator = PluginCreator;
        }

        public override void OnLoad()
        {
            var assemblypath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string settingsfilepath = Path.Join(assemblypath, ZwiftPluginSettings.DefaultSettingsFileName);

            //Load Plugin Settings
            if (File.Exists(settingsfilepath))
            {
                Log("Loading plugin settings file.", LogVerbosityLevel.INFO);
                string filedata = File.ReadAllText(settingsfilepath);
                Settings = JsonConvert.DeserializeObject<ZwiftPluginSettings>(filedata);
                Log($"BaseDir: {(Settings as ZwiftPluginSettings).BaseDir}", LogVerbosityLevel.INFO);
            }
            else
            {
                Log("Plugin Settings file not found.", LogVerbosityLevel.INFO);
                Settings = ZwiftPluginSettings.GenerateDefaultSettings() as ZwiftPluginSettings;
                Settings.SaveToFile();
            }

            //Set Context To Importer/Exporter classes
            ZwiftImporter.PluginRef = this;

            openFileDialog = new("zwift-open-file", ".gde", false); //Initialize OpenFileDialog
            //saveFileDialog = new("zwidft-save-file", ExportFormats, ExportFormatExtensions); //Initialize OpenFolderDialog
            
            //openFileDialog.SetDialogPath(assemblypath);
            openFileDialog.SetDialogPath("C:\\Program Files (x86)\\Zwift\\data\\bikes\\Frames\\CubeLitening2021");
            //saveFileDialog.SetDialogPath("G:\\Downloads");

            Log("Plugin Loaded", LogVerbosityLevel.INFO);
        }

        public override void Draw()
        {
            if (openFileDialog != null) //TODO Check if plugin loaded instead of that
            {
                if (openFileDialog.Draw(new System.Numerics.Vector2(600, 400)))
                {
                    Import(openFileDialog.GetSelectedFile());
                }
            }
        }

        public override void DrawExporters(SceneGraph scn)
        {
            
        }

        public override void DrawImporters()
        {
            if (ImGuiCore.MenuItem("Zwift Import", "", false, true))
            {
                openFileDialog.Open();
            }
        }

        public override void Export(string filepath)
        {
            throw new NotImplementedException();
        }

        public override void Import(string filepath)
        {
            ZwiftImporter.ClearState();
            try
            {
                SceneGraphNode root = ZwiftImporter.Import(filepath);
                EngineRef.ImportScene(root);
            } catch (Exception ex)
            {
                Log(ex.Message, LogVerbosityLevel.ERROR);
            }
        }

        public override void OnUnload()
        {
            
        }
    }
}
