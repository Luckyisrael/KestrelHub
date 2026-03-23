using KestrelHub.Shared.Enums;

namespace KestrelHub.Shared.Models;

public class ScanResult
{
    public ProjectType ProjectType { get; set; }
    public string PrimaryProjectPath { get; set; } = string.Empty;
    public string DotNetVersion { get; set; } = string.Empty;
}
