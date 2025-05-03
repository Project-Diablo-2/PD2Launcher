namespace PD2Shared.Models
{
    public class DdrawOptions
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Fullscreen { get; set; } = true;
        public bool Windowed { get; set; }
        public bool Maintas { get; set; }
        public bool Boxing { get; set; }
        public string MaxFps { get; set; } = "60";
        public bool Vsync { get; set; }
        public bool AdjMouse { get; set; }
        public string Shader { get; set; } = "Shaders\\xbr\\xbr-lv2-noblend.glsl";
        public int PosX { get; set; } = -32000;
        public int PosY { get; set; } = -32000;
        public string Renderer { get; set; } = "opengl";
        public bool DevMode { get; set; }
        public bool Border { get; set; }
        public string SaveSettings { get; set; } = "1";
        public bool Resizeable { get; set; }
        public bool Vhack { get; set; }
        public bool D3d9Linear { get; set; }
        public bool NoActivateApp { get; set; }
        public string MaxGameTicks { get; set; } = "-2";
        public bool HandleMouse { get; set; } = true;
        public string Hook { get; set; } = "4";
        public string MinFps { get; set; } = "0";
        public bool NonExclusive { get; set; }
        public bool SingleCpu { get; set; } = true;
    }
}