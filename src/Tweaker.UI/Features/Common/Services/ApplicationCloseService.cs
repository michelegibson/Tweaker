using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Tweaker.Core.Features.Common.Interfaces;
using Tweaker.Core.Features.Common.Models;

namespace Tweaker.UI.Features.Common.Services;

public class ApplicationCloseService : IApplicationCloseService
{
    private readonly ILogService _logService;
    private readonly ITaskProgressService _taskProgressService;
    private readonly IDialogService _dialogService;

    public Func<Task>? BeforeShutdown { get; set; }

    // Terminates the process. Replaced in tests so the test host survives.
    public Action ShutdownAction { get; set; } = DefaultShutdown;

    private static void DefaultShutdown()
    {
        try
        {
            Application.Current.Exit();
        }
        catch
        {
            Environment.Exit(0);
        }
    }

    public ApplicationCloseService(
        ILogService logService,
        ITaskProgressService taskProgressService,
        IDialogService dialogService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _taskProgressService = taskProgressService ?? throw new ArgumentNullException(nameof(taskProgressService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    public async Task<OperationResult> CheckOperationsAndCloseAsync()
    {
        try
        {
            if (BeforeShutdown != null)
            {
                try
                {
                    await BeforeShutdown.Invoke();
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error running cleanup tasks: {ex.Message}", ex);
                }
            }

            if (_taskProgressService.IsTaskRunning)
            {
                string currentOperation = _taskProgressService.CurrentStatusText ?? "an operation";

                _logService.LogInformation($"Close requested while operation in progress: {currentOperation}");

                var confirmed = (await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
                {
                    Message = $"The following operation is still running:\n\n{currentOperation}\n\n" +
                              $"Closing now may leave incomplete files or mounted drives.\n\n" +
                               $"Cancel this operation and close Modune?",
                    Title = "Warning: Operation in Progress",
                    ConfirmButtonText = "Yes, Close",
                    CancelButtonText = "Cancel",
                })).Confirmed;

                if (!confirmed)
                {
                    _logService.LogInformation("User cancelled application close due to running operation");
                    return OperationResult.Failed("User cancelled application close");
                }

                _logService.LogInformation("User confirmed close, cancelling operation...");
                _taskProgressService.CancelCurrentTask();
            }

            ShutdownAction();
            return OperationResult.Succeeded();
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error in CheckOperationsAndCloseAsync: {ex.Message}", ex);

            try
            {
                ShutdownAction();
            }
            catch
            {
                ShutdownAction();
            }
            return OperationResult.Succeeded();
        }
    }

}
