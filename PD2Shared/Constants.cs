﻿using PD2Shared.Models;

namespace PD2Shared
{
    public static class Constants
    {
        public static readonly List<string> excludedFiles = new List<string>
        { "D2.LNG", "BnetLog.txt", "ProjectDiablo.cfg", "ddraw.ini", "default.filter", "loot.filter", "UI.ini", "d2gl.yaml"};

        public static class Ddraw
        {
            public const string IniFileName = "ddraw.ini";

            public static DdrawOptions DefaultDdrawOptions => new()
            {
                Fullscreen = true,
                MaxFps = "60",
                Shader = "Shaders\\xbr\\xbr-lv2-noblend.glsl",
                PosX = -32000,
                PosY = -32000,
                Renderer = "opengl",
                SaveSettings = "1",
                Resizeable = true,
                MaxGameTicks = "-2",
                HandleMouse = true,
                Hook = "4",
                MinFps = "0",
                SingleCpu = true
            };
        }

        public static class LocalStorage
        {
            public const string LauncherArgsKey = "LauncherArgs";
            public const string DdrawOptionsKey = "DdrawOptions";
            public const string FileUpdateModelKey = "FileUpdateModel";
            public const string AuthorAndFilterKey = "AuthorAndFilterKey";
        }

        public static List<DisplayValuePair> MaxFpsPickerItems()
        {
            return new List<DisplayValuePair>
            {
                new DisplayValuePair { DisplayValue = "60 (default)", ActualValue = "60" },
                new DisplayValuePair { DisplayValue = "-1 (screen rate)", ActualValue = "-1" },
                new DisplayValuePair { DisplayValue = "unlimited", ActualValue = "0" },
                new DisplayValuePair { DisplayValue = "n = cap", ActualValue = "n" }
            };
        }

        public static List<DisplayValuePair> ModePickerItems()
        {
            return new List<DisplayValuePair>
            {
                new DisplayValuePair { DisplayValue = "Fullscreen (default)", ActualValue = "fullscreen" },
                new DisplayValuePair { DisplayValue = "Windowed", ActualValue = "windowed" },
                new DisplayValuePair { DisplayValue = "Borderless", ActualValue = "borderless" }
            };
        }

        public static List<DisplayValuePair> MaxGameTicksPickerItems()
        {
            return new List<DisplayValuePair>
            {
                new DisplayValuePair { DisplayValue = "-2 Default Refresh Rate", ActualValue = "-2" },
                new DisplayValuePair { DisplayValue = "-1 (disable)", ActualValue = "-1" },
                new DisplayValuePair { DisplayValue = "0 emulate 60hz", ActualValue = "0" },
                new DisplayValuePair { DisplayValue = "60", ActualValue = "60" },
                new DisplayValuePair { DisplayValue = "30", ActualValue = "30" },
                new DisplayValuePair { DisplayValue = "25", ActualValue = "25" },
                new DisplayValuePair { DisplayValue = "20", ActualValue = "20" },
                new DisplayValuePair { DisplayValue = "15", ActualValue = "15" }
            };
        }

        public static List<DisplayValuePair> SaveWindowPositionPickerItems()
        {
            return new List<DisplayValuePair>
            {
                new DisplayValuePair { DisplayValue = "global (default)", ActualValue = "1" },
                new DisplayValuePair { DisplayValue = "game specific", ActualValue = "2" },
                new DisplayValuePair { DisplayValue = "disable", ActualValue = "0" }
            };
        }

        public static List<DisplayValuePair> RendererPickerItems()
        {
            return new List<DisplayValuePair>
            {
                new DisplayValuePair { DisplayValue = "OpenGL (default)", ActualValue = "opengl" },
                new DisplayValuePair { DisplayValue = "Auto", ActualValue = "auto" },
                new DisplayValuePair { DisplayValue = "direct3d9", ActualValue = "direct3d9" },
                new DisplayValuePair { DisplayValue = "GDI", ActualValue = "gdi" }
            };
        }

        public static List<DisplayValuePair> HookPickerItems()
        {
            return new List<DisplayValuePair>
            {
                new DisplayValuePair { DisplayValue = "all modules (default)", ActualValue = "4" },
                new DisplayValuePair { DisplayValue = "IAT hooking", ActualValue = "1" },
                new DisplayValuePair { DisplayValue = "microsoft detours", ActualValue = "2" },
                new DisplayValuePair { DisplayValue = "IAT+detours", ActualValue = "3" },
                new DisplayValuePair { DisplayValue = "disable", ActualValue = "0" }
            };
        }

        public static List<DisplayValuePair> MinFpsPickerItems()
        {
            return new List<DisplayValuePair>
            {
                new DisplayValuePair { DisplayValue = "0 (default)", ActualValue = "0" },
                new DisplayValuePair { DisplayValue = "-1 (use max fps)", ActualValue = "-1" },
                new DisplayValuePair { DisplayValue = "5", ActualValue = "5" },
                new DisplayValuePair { DisplayValue = "10", ActualValue = "10" }
            };
        }

        public static List<DisplayValuePair> ShaderPickerItems()
        {
            return new List<DisplayValuePair>
            {
                new DisplayValuePair { DisplayValue = "xbr-lv2-noblend (default)", ActualValue = "Shaders\\xbr\\xbr-lv2-noblend.glsl" },
                new DisplayValuePair { DisplayValue = "xbr-lv2", ActualValue = "Shaders\\xbr-lv2.glsl" },
                new DisplayValuePair { DisplayValue = "xbr-lv3", ActualValue = "Shaders\\xbr\\xbr-lv3.glsl" },
                new DisplayValuePair { DisplayValue = "xbrz-freescale", ActualValue = "Shaders\\xbrz-freescale.glsl" },
                new DisplayValuePair { DisplayValue = "4xbrz", ActualValue = "Shaders\\xbrz\\4xbrz.glsl" },
                new DisplayValuePair { DisplayValue = "5xbrz", ActualValue = "Shaders\\xbrz\\5xbrz.glsl" },
                new DisplayValuePair { DisplayValue = "6xbrz", ActualValue = "Shaders\\xbrz\\6xbrz.glsl" },
                new DisplayValuePair { DisplayValue = "aa-shader-4.0", ActualValue = "Shaders\\anti-aliasing\\aa-shader-4.0.glsl" },
                new DisplayValuePair { DisplayValue = "advanced-aa", ActualValue = "Shaders\\anti-aliasing\\advanced-aa.glsl" },
                new DisplayValuePair { DisplayValue = "reverse-aa", ActualValue = "Shaders\\anti-aliasing\\reverse-aa.glsl" },
                new DisplayValuePair { DisplayValue = "bilinear", ActualValue = "Shaders\\bilinear.glsl" },
                new DisplayValuePair { DisplayValue = "bright", ActualValue = "Shaders\\bright.glsl" },
                new DisplayValuePair { DisplayValue = "crt-lottes-fast", ActualValue = "Shaders\\crt-lottes-fast.glsl" },
                new DisplayValuePair { DisplayValue = "cubic", ActualValue = "Shaders\\cubic.glsl" },
                new DisplayValuePair { DisplayValue = "scanline", ActualValue = "Shaders\\scanline.glsl" },
                new DisplayValuePair { DisplayValue = "simple-sharp-bilinear", ActualValue = "Shaders\\simple-sharp-bilinear.glsl" }
            };
        }
    }
}