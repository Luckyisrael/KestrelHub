namespace KestrelHub.Shared.Models;

public class DockerfileTemplate
{
    public string Content { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public string AssemblyName { get; set; } = string.Empty;
}
