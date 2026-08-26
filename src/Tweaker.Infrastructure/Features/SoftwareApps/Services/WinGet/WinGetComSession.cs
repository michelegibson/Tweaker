using Microsoft.Management.Deployment;
using Tweaker.Core.Features.Common.Interfaces;
using WindowsPackageManager.Interop;

namespace Tweaker.Infrastructure.Features.SoftwareApps.Services.WinGet;

/// <summary>
/// Owns the WinGet COM state (factory, package manager, lock, flags).
/// Shared singleton — injected into services that need COM access.
/// </summary>
public class WinGetComSession
{
    private readonly ILogService _logService;

    private WindowsPackageManagerFactory? _winGetFactory;
    private PackageManager? _packageManager;
    private readonly object _factoryLock = new();
    private volatile bool _isInitialized;
    private volatile bool _comInitTimedOut;

    public WinGetComSession(ILogService logService)
    {
        _logService = logService;
    }

    public PackageManager? PackageManager => _packageManager;
    public WindowsPackageManagerFactory? Factory => _winGetFactory;

    public bool ComInitTimedOut
    {
        get => _comInitTimedOut;
        set => _comInitTimedOut = value;
    }

    public bool EnsureComInitialized()
    {
        if (_isInitialized && _packageManager != null)
            return true;

        if (_comInitTimedOut)
            return false;

        lock (_factoryLock)
        {
            if (_isInitialized && _packageManager != null)
                return true;

            if (_comInitTimedOut)
                return false;

            try
            {
                // Tweaker always runs as admin with self-contained AppSdk.
                // StandardFactory + ALLOW_LOWER_TRUST_REGISTRATION is the only approach
                // that works in this configuration.
                // ElevatedFactory (winrtact.dll) hangs in self-contained mode:
                //
                _logService?.LogInformation("Initializing WinGet COM API via StandardFactory");
                _winGetFactory = new WindowsPackageManagerStandardFactory(
                    ClsidContext.Prod,
                    allowLowerTrustRegistration: true);
                _packageManager = _winGetFactory.CreatePackageManager();
                _isInitialized = true;
                _logService?.LogInformation("WinGet COM API initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Failed to initialize WinGet COM API: {ex.Message}");
                _isInitialized = false;
                _packageManager = null;
                _winGetFactory = null;
                return false;
            }
        }
    }

    public void ResetFactory()
    {
        lock (_factoryLock)
        {
            _isInitialized = false;
            _comInitTimedOut = false;
            _packageManager = null;
            _winGetFactory = null;
        }
    }
}
