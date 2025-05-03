﻿
using MadMilkman.Ini;
using PD2Shared.Interfaces;
using PD2Shared.Models;
using PD2Shared.Storage;
using PD2Shared;
using System.Diagnostics;
using System.IO;

namespace PD2Shared.Helpers
{
    public class DDrawHelpers
    {
        private readonly ILocalStorage _localStorage;

        public DDrawHelpers(ILocalStorage localStorage)
        {
            _localStorage = localStorage;
        }

        public DdrawOptions ReadDdrawOptions()
        {
           

            string ddrawIniPath = GetDdrawIniPath();
            IniFile iniFile = new IniFile();
            try
            {
                using (Stream iniStream = new FileStream(ddrawIniPath, FileMode.Open, FileAccess.Read))
                {
                    iniFile.Load(iniStream);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading ddraw.ini: {ex.Message}");
                return new DdrawOptions(); 
            }

            // Read settings from iniFile and update DdrawOptions
            DdrawOptions dDrawOptions = ParseDdrawOptionsFromIni(iniFile);

            return dDrawOptions;
        }

        private DdrawOptions ParseDdrawOptionsFromIni(IniFile iniFile)
        {
            var ddrawSection = iniFile.Sections["ddraw"];
            var defaultDdrawOptions = Constants.Ddraw.DefaultDdrawOptions;

            if (ddrawSection == null)
            {
                // If the ddraw section is not found, return the default options
                Debug.WriteLine("ddraw section not found in the ini file. Using default settings.");
                return defaultDdrawOptions;
            }

            var options = new DdrawOptions
            {
                // Read and parse each option from the ini file, like:
                Width = int.TryParse(ddrawSection.Keys["width"]?.Value, out int widthValue) ? widthValue : defaultDdrawOptions.Width,
                Height = int.TryParse(ddrawSection.Keys["height"]?.Value, out int heightValue) ? heightValue : defaultDdrawOptions.Height,
                Fullscreen = bool.TryParse(ddrawSection.Keys["fullscreen"]?.Value.ToLower(), out bool fullscreenValue) ? fullscreenValue : defaultDdrawOptions.Fullscreen,
                Windowed = bool.TryParse(ddrawSection.Keys["windowed"]?.Value.ToLower(), out bool windowedValue) ? windowedValue : defaultDdrawOptions.Windowed,
                Maintas = bool.TryParse(ddrawSection.Keys["maintas"]?.Value.ToLower(), out bool maintasValue) ? maintasValue : defaultDdrawOptions.Maintas,
                Boxing = bool.TryParse(ddrawSection.Keys["boxing"]?.Value.ToLower(), out bool boxingValue) ? boxingValue : defaultDdrawOptions.Boxing,
                MaxFps = ddrawSection.Keys["maxfps"]?.Value ?? defaultDdrawOptions.MaxFps,
                Vsync = bool.TryParse(ddrawSection.Keys["vsync"]?.Value.ToLower(), out bool vsyncValue) ? vsyncValue : defaultDdrawOptions.Vsync,
                AdjMouse = bool.TryParse(ddrawSection.Keys["adjmouse"]?.Value.ToLower(), out bool adjmouseValue) ? adjmouseValue : defaultDdrawOptions.AdjMouse,
                Shader = ddrawSection.Keys["shader"]?.Value ?? defaultDdrawOptions.Shader,
                PosX = int.TryParse(ddrawSection.Keys["posX"]?.Value, out int posXValue) ? posXValue : defaultDdrawOptions.PosX,
                PosY = int.TryParse(ddrawSection.Keys["posY"]?.Value, out int posYValue) ? posYValue : defaultDdrawOptions.PosY,
                Renderer = ddrawSection.Keys["renderer"]?.Value ?? defaultDdrawOptions.Renderer,
                DevMode = bool.TryParse(ddrawSection.Keys["devmode"]?.Value.ToLower(), out bool devmodeValue) ? devmodeValue : defaultDdrawOptions.DevMode,
                Border = bool.TryParse(ddrawSection.Keys["border"]?.Value.ToLower(), out bool borderValue) ? borderValue : defaultDdrawOptions.Border,
                SaveSettings = ddrawSection.Keys["savesettings"]?.Value ?? defaultDdrawOptions.SaveSettings,
                Resizeable = bool.TryParse(ddrawSection.Keys["resizeable"]?.Value.ToLower(), out bool resizeableValue) ? resizeableValue : defaultDdrawOptions.Resizeable,
                Vhack = bool.TryParse(ddrawSection.Keys["vhack"]?.Value.ToLower(), out bool vhackValue) ? vhackValue : defaultDdrawOptions.Vhack,
                D3d9Linear = bool.TryParse(ddrawSection.Keys["d3d9linear"]?.Value.ToLower(), out bool d3d9linearValue) ? d3d9linearValue : defaultDdrawOptions.D3d9Linear,
                NoActivateApp = bool.TryParse(ddrawSection.Keys["noactivateapp"]?.Value.ToLower(), out bool noactivateappValue) ? noactivateappValue : defaultDdrawOptions.NoActivateApp,
                MaxGameTicks = ddrawSection.Keys["maxgameticks"]?.Value ?? defaultDdrawOptions.MaxGameTicks,
                HandleMouse = bool.TryParse(ddrawSection.Keys["handlemouse"]?.Value.ToLower(), out bool handlemouseValue) ? handlemouseValue : defaultDdrawOptions.HandleMouse,
                Hook = ddrawSection.Keys["hook"]?.Value ?? defaultDdrawOptions.Hook,
                MinFps = ddrawSection.Keys["minfps"]?.Value ?? defaultDdrawOptions.MinFps,
                NonExclusive = bool.TryParse(ddrawSection.Keys["nonexclusive"]?.Value.ToLower(), out bool nonexclusiveValue) ? nonexclusiveValue : defaultDdrawOptions.NonExclusive,
                SingleCpu = bool.TryParse(ddrawSection.Keys["singlecpu"]?.Value.ToLower(), out bool singlecpuValue) ? singlecpuValue : defaultDdrawOptions.SingleCpu
            };
            return options;
        }

        private string GetDdrawIniPath()
        {
            FileUpdateModel fileModel = _localStorage.LoadSection<FileUpdateModel>(StorageKey.FileUpdateModel);
            String envBucket = fileModel.FilePath;
            var pd2Path = Directory.GetCurrentDirectory();
            return Path.Combine(pd2Path, "ddraw.ini");
        }

        public void WriteDdrawOptions()
        {
            DdrawOptions options = _localStorage.LoadSection<DdrawOptions>(StorageKey.DdrawOptions);
            string ddrawIniPath = GetDdrawIniPath();
            IniFile iniFile = new IniFile();

            // Load the ini file if it exists
            if (File.Exists(ddrawIniPath))
            {
                using (var stream = File.OpenRead(ddrawIniPath))
                {
                    iniFile.Load(stream);
                }
            }

            // Set default options if needed
            var defaultDdrawOptions = Constants.Ddraw.DefaultDdrawOptions;

            // Ensure the 'ddraw' section exists
            IniSection ddrawSection = iniFile.Sections["ddraw"] ?? iniFile.Sections.Add("ddraw");

            void SetOption<T>(string keyName, T value, T defaultValue)
            {
                // Use ToStringInvariant if the method is available, or just ToString otherwise
                string valueString = (value != null) ? value.ToString().ToLowerInvariant() : defaultValue.ToString().ToLowerInvariant();
                if (ddrawSection.Keys[keyName] != null)
                {
                    ddrawSection.Keys[keyName].Value = valueString;
                }
                else
                {
                    ddrawSection.Keys.Add(keyName, valueString);
                }
            }

            // Set or update each option
            SetOption("width", options.Width, defaultDdrawOptions.Width);
            SetOption("height", options.Height, defaultDdrawOptions.Height);
            SetOption("fullscreen", options.Fullscreen, defaultDdrawOptions.Fullscreen);
            SetOption("windowed", options.Windowed, defaultDdrawOptions.Windowed);
            SetOption("maintas", options.Maintas, defaultDdrawOptions.Maintas);
            SetOption("boxing", options.Boxing, defaultDdrawOptions.Boxing);
            SetOption("maxfps", options.MaxFps, defaultDdrawOptions.MaxFps);
            SetOption("vsync", options.Vsync, defaultDdrawOptions.Vsync);
            SetOption("adjmouse", options.AdjMouse, defaultDdrawOptions.AdjMouse);
            SetOption("shader", options.Shader, defaultDdrawOptions.Shader);
            SetOption("posX", options.PosX, defaultDdrawOptions.PosX);
            SetOption("posY", options.PosY, defaultDdrawOptions.PosY);
            SetOption("renderer", options.Renderer, defaultDdrawOptions.Renderer);
            SetOption("devmode", options.DevMode, defaultDdrawOptions.DevMode);
            SetOption("border", options.Border, defaultDdrawOptions.Border);
            SetOption("savesettings", options.SaveSettings, defaultDdrawOptions.SaveSettings);
            SetOption("resizeable", options.Resizeable, defaultDdrawOptions.Resizeable);
            SetOption("vhack", options.Vhack, defaultDdrawOptions.Vhack);
            SetOption("d3d9linear", options.D3d9Linear, defaultDdrawOptions.D3d9Linear);
            SetOption("noactivateapp", options.NoActivateApp, defaultDdrawOptions.NoActivateApp);
            SetOption("maxgameticks", options.MaxGameTicks, defaultDdrawOptions.MaxGameTicks);
            SetOption("handlemouse", options.HandleMouse, defaultDdrawOptions.HandleMouse);
            SetOption("hook", options.Hook, defaultDdrawOptions.Hook);
            SetOption("minfps", options.MinFps, defaultDdrawOptions.MinFps);
            SetOption("nonexclusive", options.NonExclusive, defaultDdrawOptions.NonExclusive);
            SetOption("singlecpu", options.SingleCpu, defaultDdrawOptions.SingleCpu);

            // Save the ini file
            using (var stream = File.Open(ddrawIniPath, FileMode.Create))
            {
                iniFile.Save(stream);
            }
        }
    }
}