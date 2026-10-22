namespace PD2Launcherv2.Utils.Gl
{
    public class GlCtxInfo
    {
        public GlCtxInfo(
            Version? version,
            string glVendor,
            string glRenderer,
            string glVersion,
            string glShadingLanguageVersion,

            bool? isForwardCompatible,
            bool? isDebug,
            bool? isRobust,
            bool? isNoError,

            bool? isCore,
            bool? isCompatibility
        )
        {
            Version = version;

            GlVendor = glVendor;
            GlRenderer = glRenderer;
            GlVersion = glVersion;
            GlShadingLanguageVersion = glShadingLanguageVersion;

            IsForwardCompatible = isForwardCompatible;
            IsDebug = isDebug;
            IsRobust = isRobust;
            IsNoError = isNoError;

            IsCore = isCore;
            IsCompatibility = isCompatibility;
        }

        public Version? Version { get; }

        public string GlVendor { get; }
        public string GlRenderer { get; }
        public string GlVersion { get; }
        public string GlShadingLanguageVersion { get; }

        public bool? IsForwardCompatible { get; }
        public bool? IsDebug { get; }
        public bool? IsRobust { get; }
        public bool? IsNoError { get; }

        public bool? IsCore { get; }
        public bool? IsCompatibility { get; }

        public string[] ToLines()
        {
            return new string[] {
                $"{(Version?.ToString() ?? "Unknown version")}{(IsCore == true ? " Core" : IsCompatibility == true ? " Compatibility" : string.Empty)}{(IsForwardCompatible == true ? " Forward-compatible" : string.Empty)}",
                $"GL_VERSION: {GlVersion}",
                $"GL_RENDERER: {GlRenderer}",
                $"GL_VENDOR: {GlVendor}",
                $"GL_SHADING_LANGUAGE_VERSION: {GlShadingLanguageVersion}"
            };
        }

        public override string ToString()
        {
            return string.Join('\n', ToLines());
        }
    }
}
