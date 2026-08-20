namespace Tweaker.Core.Features.Common.Constants;

public static class ScriptPaths
{
    public static readonly string ScriptsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Tweaker", "Scripts");

    /// <summary>Literal path for embedding in generated PowerShell scripts.</summary>
    public const string ScriptsDirectoryLiteral = @"C:\ProgramData\Tweaker\Scripts";

    /// <summary>Literal path for embedding in generated PowerShell scripts.</summary>
    public const string LogsDirectoryLiteral = @"C:\ProgramData\Tweaker\Logs";

    /// <summary>Literal path for embedding in generated PowerShell scripts.</summary>
    public const string UnattendScriptPath = @"C:\ProgramData\Tweaker\Unattend\Scripts\Tweakerments.ps1";

    /// <summary>Literal path for embedding in generated PowerShell scripts.</summary>
    public const string PowerShellExePath = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";
}