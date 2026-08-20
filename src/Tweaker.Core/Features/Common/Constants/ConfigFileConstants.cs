using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tweaker.Core.Features.Common.Constants;

public static class ConfigFileConstants
{
    public const string FileExtension = ".tweaker";
    public const string FileFilter = "Modune Configuration Files";
    public const string FilePattern = "*.tweaker";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
