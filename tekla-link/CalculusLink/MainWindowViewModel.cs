using System.IO;
using System.Reflection;

using CalculusModel;

using CalculusLink.Bridge;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Serilog;

using TeklaAPIExtensions.Model;

using Wpf.Ui;

using TSM = Tekla.Structures.Model;

namespace CalculusLink;

public enum CoordinateSystemType
{
    Global,
    Workplane,
}

public partial class MainWindowViewModel(ISnackbarService snackbarService) : ObservableObject
{
    private readonly SnackbarHelper _snackbar = new(snackbarService);

    [ObservableProperty]
    public partial bool IsImporting { get; set; } = false;
    [ObservableProperty]
    public partial bool IsExporting { get; set; } = false;

    [ObservableProperty]
    public partial CoordinateSystemType ImportCoordinateSystem { get; set; } = CoordinateSystemType.Global;
    [ObservableProperty]
    public partial CoordinateSystemType ExportCoordinateSystem { get; set; } = CoordinateSystemType.Global;
    public List<CoordinateSystemType> CoordinateSystemOptions => [.. Enum.GetValues(typeof(CoordinateSystemType)).Cast<CoordinateSystemType>()];

    // --- Direct (bridge) transfer state ----------------------------------------

    private CalculusBridgeClient? _bridgeClient;

    // --- Which transfer the window shows (the tabs at the top) -----------------

    [ObservableProperty]
    public partial bool ShowFileTransfer { get; set; } = false;

    /// <summary>Direct transfer is the first tab; the window connects on open (MainWindow).</summary>
    [ObservableProperty]
    public partial bool ShowDirectTransfer { get; set; } = true;

    partial void OnShowDirectTransferChanged(bool value)
    {
        if (value) RefreshCommand.Execute(null);
    }

    [ObservableProperty]
    public partial int BridgePort { get; set; } = CalculusBridgeClient.DefaultPort;

    partial void OnBridgePortChanged(int value) => RefreshCommand.Execute(null);

    /// <summary>Agent token, when the Calculus desktop app requires one (empty otherwise).</summary>
    [ObservableProperty]
    public partial string BridgeToken { get; set; } = string.Empty;

    partial void OnBridgeTokenChanged(string value) => RefreshCommand.Execute(null);

    /// <summary>Port and token fields shown (the gear); opened by a refused token.</summary>
    [ObservableProperty]
    public partial bool ShowConnectionSettings { get; set; } = false;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TransferToCalculusCommand))]
    [NotifyCanExecuteChangedFor(nameof(TransferToTeklaCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewToCalculusCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewToTeklaCommand))]
    public partial bool BridgeRunning { get; set; } = false;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TransferToCalculusCommand))]
    [NotifyCanExecuteChangedFor(nameof(TransferToTeklaCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewToCalculusCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewToTeklaCommand))]
    public partial bool EditorConnected { get; set; } = false;
    /// <summary>The models open in Calculus, as the bridge lists them.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSeveralEditors))]
    public partial List<BridgeEditor> Editors { get; set; } = [];

    /// <summary>The model picker shows only when there is something to pick.</summary>
    public bool HasSeveralEditors => Editors.Count > 1;

    /// <summary>The model transfers go to. Null until one is picked (or the only one).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TransferToCalculusCommand))]
    [NotifyCanExecuteChangedFor(nameof(TransferToTeklaCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewToCalculusCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewToTeklaCommand))]
    public partial BridgeEditor? SelectedEditor { get; set; }

    partial void OnSelectedEditorChanged(BridgeEditor? value)
    {
        if (_bridgeClient is not null) _bridgeClient.Editor = value?.Id;
        EditorConnected = value is not null;
        BridgeStatusText = value is null
            ? (Editors.Count == 0 ? "Calculus is running — no model is open" : "Pick the model to transfer with")
            : $"Connected to {value}";
    }

    [ObservableProperty]
    public partial string BridgeStatusText { get; set; } = "Not connected";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TransferToCalculusCommand))]
    [NotifyCanExecuteChangedFor(nameof(TransferToTeklaCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewToCalculusCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewToTeklaCommand))]
    public partial bool IsTransferring { get; set; } = false;

    // --- What a transfer may do beyond add and update (per direction) ----------

    [ObservableProperty]
    public partial bool OverwriteConflictsInCalculus { get; set; } = false;

    [ObservableProperty]
    public partial bool RemoveGoneInCalculus { get; set; } = false;

    [ObservableProperty]
    public partial bool OverwriteConflictsInTekla { get; set; } = false;

    [ObservableProperty]
    public partial bool RemoveGoneInTekla { get; set; } = false;

    readonly Version _appVersion = Assembly.GetExecutingAssembly().GetName().Version;
    public string AppVersion => "v" + _appVersion.Major + "." + _appVersion.Minor;

    [RelayCommand(CanExecute = nameof(CanImport))]
    Task Import(string[] files)
    {
        return Task.Run(() =>
        {
            var teklaModel = new TSM.Model();
            if (!teklaModel.GetConnectionStatus())
            {
                _snackbar.ShowErrorMessage("Tekla Structures is not connected");
                return;
            }

            Log.Information("Importing {FileCount} files: ", files.Length, string.Join(", ", files));
            IsImporting = true;

            // Store the current work plane to restore it after export
            var workPlaneHandler = teklaModel.GetWorkPlaneHandler();
            var currentPlane = workPlaneHandler.GetCurrentTransformationPlane();
            // Set the global work plane to ensure consistent coordinates during export
            if (ImportCoordinateSystem == CoordinateSystemType.Global)
            {
                workPlaneHandler.SetCurrentTransformationPlane(new TSM.TransformationPlane());
            }

            int successCount = 0;
            foreach (string file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var model = CalculusModelSerializer.Deserialize(json);

                    if (model is null)
                    {
                        Log.Error("Failed to deserialize file: {File}", file);
                        continue;
                    }

                    CalculusModelInserter.UpdateTeklaModel(model, teklaModel);

                    successCount++;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error importing file: {File}", file);
                }
            }

            // Restore the original work plane
            workPlaneHandler.SetCurrentTransformationPlane(currentPlane);

            IsImporting = false;
            if (successCount == files.Length)
            {
                _snackbar.ShowSuccessMessage("All files imported");
            }
            else if (successCount > 0)
            {
                _snackbar.ShowWarningMessage($"Imported {successCount}/{files.Length} files");
            }
            else
            {
                _snackbar.ShowErrorMessage("No files were imported");
            }
            Log.Information("Imported {SuccessCount}/{TotalCount} files", successCount, files.Length);
            TSM.Operations.Operation.DisplayPrompt($"Calculus Link: Imported {successCount}/{files.Length} files");
        });
    }
    bool CanImport() => !IsImporting && !IsExporting;

    [RelayCommand(CanExecute = nameof(CanExport))]
    Task Export()
    {
        return Task.Run(() =>
        {
            var teklaModel = new TSM.Model();
            if (!teklaModel.GetConnectionStatus())
            {
                _snackbar.ShowErrorMessage("Tekla Structures is not connected");
                return;
            }

            Log.Information("Exporting selected objects");
            IsExporting = true;

            // Store the current work plane to restore it after export
            var workPlaneHandler = teklaModel.GetWorkPlaneHandler();
            var currentPlane = workPlaneHandler.GetCurrentTransformationPlane();
            try
            {
                var selected = new TSM.UI.ModelObjectSelector().GetSelectedObjects().OfType<TSM.ModelObject>().ToList();
                if (selected.Count == 0)
                {
                    throw new Exception("No objects selected for export");
                }

                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Calculus Files (*.calculus)|*.calculus|All Files (*.*)|*.*",
                    FileName = "model.calculus"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    Log.Information("Exporting {Count} objects", selected.Count);

                    // Set the global work plane to ensure consistent coordinates during export
                    if (ExportCoordinateSystem == CoordinateSystemType.Global)
                    {
                        workPlaneHandler.SetCurrentTransformationPlane(new TSM.TransformationPlane());
                    }

                    // Build the Calculus model from the selected Tekla objects
                    var calculusModel = CalculusModelBuilder.FromTeklaObjects(selected);
                    calculusModel.Name = saveFileDialog.SafeFileName.Replace(".calculus", "");

                    // Serialize the Calculus model to JSON and save it to the selected file
                    var json = CalculusModelSerializer.Serialize(calculusModel);
                    File.WriteAllText(saveFileDialog.FileName, json);

                    // Show success message and log the export
                    _snackbar.ShowSuccessMessage("Export completed");
                    Log.Information("Exported {Count} objects.", calculusModel.Objects.Count);
                    TSM.Operations.Operation.DisplayPrompt($"Calculus Link: Exported completed");

                    // Open folder after export
                    var folderPath = Path.GetDirectoryName(saveFileDialog.FileName);
                    if (folderPath != null)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = folderPath,
                            UseShellExecute = true,
                            Verb = "open"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _snackbar.ShowErrorMessage(ex.Message);
                Log.Error(ex, "Error during export");
            }
            finally
            {
                // Restore the original work plane
                workPlaneHandler.SetCurrentTransformationPlane(currentPlane);

                IsExporting = false;
                Log.Information("Export finished");
            }
        });
    }
    bool CanExport() => !IsImporting && !IsExporting;

    [RelayCommand(CanExecute = nameof(CanCheckUpdate))]
    Task CheckUpdate()
    {
        return Updater.CheckForUpdates(true);
    }
    bool CanCheckUpdate() => false; // Disable the update check for now, as we don't have an update mechanism in place

    // --- Direct (bridge) transfer commands -------------------------------------

    private int _refreshGeneration;

    /// <summary>
    /// Ask the Calculus desktop app whether it runs and which models are open, and
    /// keep port + token for the transfers. There is no lasting connection — each
    /// transfer is its own request — so this is a refresh, run when the window
    /// opens or is activated (MainWindow), when the port or token change, and from
    /// the refresh button. Runs may overlap (typing a port); only the latest
    /// one's answer is applied.
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    async Task Refresh()
    {
        var generation = ++_refreshGeneration;
        var client = new CalculusBridgeClient(BridgePort, BridgeToken);
        var status = await client.GetStatusAsync();
        if (generation != _refreshGeneration) return;

        if (status is null || !status.Running)
        {
            _bridgeClient = null;
            BridgeRunning = false;
            Editors = [];
            SelectedEditor = null;
            EditorConnected = false;
            BridgeStatusText = $"Calculus not found on port {BridgePort}";
            return;
        }

        // The status probe is open to anyone; the operations are not.
        var authorized = await client.IsAuthorizedAsync();
        if (generation != _refreshGeneration) return;
        if (!authorized)
        {
            _bridgeClient = null;
            BridgeRunning = true;
            Editors = [];
            SelectedEditor = null;
            EditorConnected = false;
            BridgeStatusText = string.IsNullOrWhiteSpace(BridgeToken)
                ? "Calculus requires a token: paste it under the gear"
                : "Calculus refused the token: check it under the gear";
            ShowConnectionSettings = true;
            return;
        }

        _bridgeClient = client;
        BridgeRunning = true;
        var previous = SelectedEditor?.Id;
        Editors = status.Editors;
        // Keep the pick across reconnects; with one model open there is nothing
        // to pick; with several the user chooses.
        SelectedEditor = Editors.FirstOrDefault(e => e.Id == previous)
            ?? (Editors.Count == 1 ? Editors[0] : null);
        if (SelectedEditor is null)
        {
            EditorConnected = false;
            BridgeStatusText = Editors.Count == 0
                ? "Calculus is running — no model is open"
                : $"{Editors.Count} models open: pick the one to transfer with";
        }
    }

    // --- Direct (bridge) transfer: preview, then apply (docs/tekla-link-sync.md) ---

    [ObservableProperty]
    public partial bool HasPreviewToCalculus { get; set; }
    [ObservableProperty]
    public partial string PreviewToCalculusText { get; set; } = string.Empty;
    [ObservableProperty]
    public partial List<SyncItem> PreviewToCalculusItems { get; set; } = [];

    [ObservableProperty]
    public partial bool HasPreviewToTekla { get; set; }
    [ObservableProperty]
    public partial string PreviewToTeklaText { get; set; } = string.Empty;
    [ObservableProperty]
    public partial List<SyncItem> PreviewToTeklaItems { get; set; } = [];

    private SyncPlan? _planToCalculus;
    private DateTime _planToCalculusAt;
    private SyncPlan? _planToTekla;
    private DateTime _planToTeklaAt;
    /// <summary>A preview this recent is what Apply acts on; older, it classifies again.</summary>
    private static readonly TimeSpan PlanFreshness = TimeSpan.FromSeconds(60);

    private sealed class PreviewOutcome(SyncPlan plan, bool coloured)
    {
        public SyncPlan Plan { get; } = plan;
        public bool Coloured { get; } = coloured;
    }

    /// <summary>
    /// Run <paramref name="body"/> against Tekla and the connected editor on a
    /// worker thread: the Tekla selection resolved, the work plane set per
    /// <paramref name="coordinateSystem"/> and restored after. Errors go to the
    /// snackbar; null means it did not run or failed.
    /// </summary>
    private async Task<T?> OnTekla<T>(
        CoordinateSystemType coordinateSystem,
        Func<CalculusBridgeClient, List<TSM.ModelObject>, TSM.Model, Task<T>> body,
        string failure) where T : class
    {
        if (_bridgeClient is null)
        {
            _snackbar.ShowErrorMessage("Not connected to Calculus");
            return null;
        }
        var client = _bridgeClient;
        IsTransferring = true;
        try
        {
            return await Task.Run(async () =>
            {
                var teklaModel = new TSM.Model();
                if (!teklaModel.GetConnectionStatus())
                {
                    throw new InvalidOperationException("Tekla Structures is not connected");
                }
                var selected = new TSM.UI.ModelObjectSelector().GetSelectedObjects()
                    .OfType<TSM.ModelObject>().ToList();
                var workPlaneHandler = teklaModel.GetWorkPlaneHandler();
                var currentPlane = workPlaneHandler.GetCurrentTransformationPlane();
                if (coordinateSystem == CoordinateSystemType.Global)
                {
                    workPlaneHandler.SetCurrentTransformationPlane(new TSM.TransformationPlane());
                }
                try
                {
                    return await body(client, selected, teklaModel);
                }
                finally
                {
                    workPlaneHandler.SetCurrentTransformationPlane(currentPlane);
                }
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, failure);
            _snackbar.ShowErrorMessage(ex.Message);
            return null;
        }
        finally
        {
            IsTransferring = false;
        }
    }

    private static string PreviewText(SyncPlan plan, bool coloured) =>
        Capitalize(plan.Summary()) + (coloured
            ? ". Highlighted in both models in the colours below."
            : $". Too many objects to highlight (over {SyncPreview.MaxColoured}); the list has them all.");

    private static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

    [RelayCommand(CanExecute = nameof(CanTransfer))]
    async Task PreviewToCalculus()
    {
        var outcome = await OnTekla(ExportCoordinateSystem, async (client, selected, teklaModel) =>
        {
            if (selected.Count == 0) throw new InvalidOperationException("No objects selected in Tekla");
            var plan = await BridgeTransferService.ClassifyToCalculusAsync(client, selected, teklaModel);
            return new PreviewOutcome(plan, await SyncPreview.ShowAsync(client, plan));
        }, "Preview to Calculus failed");
        if (outcome is null) return;
        _planToCalculus = outcome.Plan;
        _planToCalculusAt = DateTime.UtcNow;
        PreviewToCalculusItems = outcome.Plan.Items.Where(i => i.State != SyncState.Unchanged).ToList();
        PreviewToCalculusText = PreviewText(outcome.Plan, outcome.Coloured);
        HasPreviewToCalculus = true;
    }

    [RelayCommand(CanExecute = nameof(CanTransfer))]
    async Task PreviewToTekla()
    {
        var outcome = await OnTekla(ImportCoordinateSystem, async (client, selected, teklaModel) =>
        {
            var plan = await BridgeTransferService.ClassifyToTeklaAsync(client, selected, teklaModel);
            if (plan.Items.Count == 0) throw new InvalidOperationException("Nothing is selected in Calculus");
            return new PreviewOutcome(plan, await SyncPreview.ShowAsync(client, plan));
        }, "Preview to Tekla failed");
        if (outcome is null) return;
        _planToTekla = outcome.Plan;
        _planToTeklaAt = DateTime.UtcNow;
        PreviewToTeklaItems = outcome.Plan.Items.Where(i => i.State != SyncState.Unchanged).ToList();
        PreviewToTeklaText = PreviewText(outcome.Plan, outcome.Coloured);
        HasPreviewToTekla = true;
    }

    [RelayCommand]
    async Task ClearPreview()
    {
        if (_bridgeClient is not null)
        {
            try { await SyncPreview.ClearAsync(_bridgeClient); }
            catch (Exception ex) { Log.Warning(ex, "Clearing the preview failed"); }
        }
        ForgetPreviews();
    }

    private void ForgetPreviews()
    {
        _planToCalculus = null;
        _planToTekla = null;
        HasPreviewToCalculus = false;
        HasPreviewToTekla = false;
        PreviewToCalculusItems = [];
        PreviewToTeklaItems = [];
    }

    [RelayCommand(CanExecute = nameof(CanTransfer))]
    async Task TransferToCalculus()
    {
        var fresh = _planToCalculus is not null && DateTime.UtcNow - _planToCalculusAt < PlanFreshness
            ? _planToCalculus : null;
        var options = new SyncOptions
        {
            OverwriteConflicts = OverwriteConflictsInCalculus,
            RemoveGone = RemoveGoneInCalculus,
        };
        var result = await OnTekla(ExportCoordinateSystem, async (client, selected, teklaModel) =>
        {
            var plan = fresh ?? await BridgeTransferService.ClassifyToCalculusAsync(client, selected, teklaModel);
            if (plan.Items.Count == 0)
            {
                throw new InvalidOperationException(selected.Count == 0
                    ? "No objects selected in Tekla"
                    : "No transferable objects in the selection");
            }
            await SyncPreview.ClearAsync(client);
            return await BridgeTransferService.ApplyToCalculusAsync(client, plan, options, teklaModel);
        }, "Direct transfer to Calculus failed");
        if (result is null) return;
        ForgetPreviews();
        var message = $"To Calculus: {result}";
        _snackbar.ShowSuccessMessage(message);
        TSM.Operations.Operation.DisplayPrompt($"Calculus Link: {message}");
    }

    [RelayCommand(CanExecute = nameof(CanTransfer))]
    async Task TransferToTekla()
    {
        var fresh = _planToTekla is not null && DateTime.UtcNow - _planToTeklaAt < PlanFreshness
            ? _planToTekla : null;
        var options = new SyncOptions
        {
            OverwriteConflicts = OverwriteConflictsInTekla,
            RemoveGone = RemoveGoneInTekla,
        };
        var result = await OnTekla(ImportCoordinateSystem, async (client, selected, teklaModel) =>
        {
            var plan = fresh ?? await BridgeTransferService.ClassifyToTeklaAsync(client, selected, teklaModel);
            if (plan.Items.Count == 0) throw new InvalidOperationException("Nothing is selected in Calculus");
            await SyncPreview.ClearAsync(client);
            return await BridgeTransferService.ApplyToTeklaAsync(client, plan, options, teklaModel);
        }, "Direct transfer to Tekla failed");
        if (result is null) return;
        ForgetPreviews();
        var message = $"To Tekla: {result}";
        _snackbar.ShowSuccessMessage(message);
        TSM.Operations.Operation.DisplayPrompt($"Calculus Link: {message}");
    }

    bool CanTransfer() => BridgeRunning && EditorConnected && !IsTransferring;
}
