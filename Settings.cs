using System.Text.Json.Serialization;

namespace CacheCow;

public class AppConfig
{
    public UpdateConfig UpdateSettings { get; set; } = new();
    public OptimizationConfig Optimization { get; set; } = new();
    public DuplicateConfig DuplicateScanner { get; set; } = new();
    public CacheTarget[] Targets { get; set; } = [];
}

public class UpdateConfig
{
    public bool EnableAutoUpdate { get; set; }
    public string ReleaseChannel { get; set; } = string.Empty;
}

public class OptimizationConfig
{
    public bool FlushDnsCache { get; set; }
}

public class DuplicateConfig
{
    public bool Enabled { get; set; }
    public string[] TargetDirectories { get; set; } = [];
    public string[] TargetExtensions { get; set; } = [];
}

public class CacheTarget
{
    public string Name { get; set; } = string.Empty;
    public string WindowsPath { get; set; } = string.Empty;
    public string MacPath { get; set; } = string.Empty;
    public string LinuxPath { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsProtected { get; set; }
}

[JsonSerializable(typeof(AppConfig))]
internal partial class AppConfigContext : JsonSerializerContext { }
