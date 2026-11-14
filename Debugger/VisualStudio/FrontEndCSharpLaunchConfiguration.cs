using System.Runtime.Serialization;

namespace FrontEndCSharp.VisualStudio;

[DataContract]
internal sealed class FrontEndCSharpLaunchConfiguration
{
    [DataMember(Name = "type", Order = 0)]
    public string Type { get; set; } = string.Empty;

    [DataMember(Name = "request", Order = 1)]
    public string Request { get; set; } = string.Empty;

    [DataMember(Name = "name", Order = 2)]
    public string Name { get; set; } = string.Empty;

    [DataMember(Name = "program", Order = 3)]
    public string Program { get; set; } = string.Empty;

    [DataMember(Name = "browserPath", Order = 4)]
    public string BrowserPath { get; set; } = string.Empty;

    [DataMember(Name = "browserHeadless", Order = 5)]
    public bool BrowserHeadless { get; set; }

    [DataMember(Name = "browserUrl", Order = 6, EmitDefaultValue = false)]
    public string? BrowserUrl { get; set; }

    [DataMember(Name = "generatedOutputPath", Order = 7)]
    public string GeneratedOutputPath { get; set; } = string.Empty;

    [DataMember(Name = "debugManifestPath", Order = 8)]
    public string DebugManifestPath { get; set; } = string.Empty;

    [DataMember(Name = "editAndContinuePipe", Order = 9)]
    public string EditAndContinuePipe { get; set; } = string.Empty;

    [DataMember(Name = "editAndContinueToken", Order = 10)]
    public string EditAndContinueToken { get; set; } = string.Empty;
}
