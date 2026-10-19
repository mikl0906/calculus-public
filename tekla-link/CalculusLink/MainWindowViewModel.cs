using System.Collections;
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
using TSUI = Tekla.Structures.Model.UI;

namespace CalculusLink;

public enum CoordinateSystemType
{
    Global,
    Workplane,
}

public partial class MainWindowViewModel : ObservableObject
{
    private readonly SnackbarHelper _snackbar;

    public MainWindowViewModel(ISnackbarService snackbarService)
    {
        _snackbar = new SnackbarHelper(snackbarService);
        foreach (var direction in new[] { ToCalculus, ToTekla })
        {
            var d = direction;
            d.SourceChanged += () => { if (d.HasPreview) CancelPreviewCommand.Execute(d); };
            d.SelectionChanged += OnPreviewRowsSelected;
            d.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TransferDirectionViewModel.HasPreview)) ApplyCommand.NotifyCanExecuteChanged();
            };
        }
    }

    [ObservableProperty]
    public partial bool IsImporting { get; set; } = false;
    [ObservableProperty]
    public partial bool IsExporting { get; set; } = false;

    [ObservableProperty]
    public partial CoordinateSystemType ImportCoordinateSystem { get; set; } = CoordinateSystemType.Global;
    [ObservableProperty]
    public partial CoordinateSystemType ExportCoordinateSystem { get; set; } = CoordinateSystemType.Global;
    /// <summary>What the file export reads from Tekla; a .calculus file is always imported whole.</summary>
    [ObservableProperty]
    public partial TransferScope ExportScope { get; set; } = TransferScope.Selected;
    public List<CoordinateSystemType> CoordinateSystemOptions => [.. Enum.GetValues(typeof(CoordinateSystemType)).Cast<CoordinateSystemType>()];
    public List<ScopeOption> ScopeOptions { get; } =
    [
        new(TransferScope.Selected, "Selected"),
        new(TransferScope.WholeModel, "Whole model"),
    ];

    // --- Direct (bridge) transfer state ----------------------------------------

    private CalculusBridgeClient? _bridgeClient;

    /// <summary>The Tekla → Calculus card.</summary>
    public TransferDirectionViewModel ToCalculus { get; } = new(SyncDirection.TeklaToCalculus);
    /// <summary>The Calculus → Tekla card.</summary>
    public TransferDirectionViewModel ToTekla { get; } = new(SyncDirection.CalculusToTekla);

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
    [NotifyCanExecuteChangedFor(nameof(PreviewCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial bool BridgeRunning { get; set; } = false;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviewCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial bool EditorConnected { get; set; } = false;
    /// <summary>The models open in Calculus, as the bridge lists them.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSeveralEditors))]
    public partial List<BridgeEditor> Editors { get; set; } = [];

    /// <summary>The model picker shows only when there is something to pick.</summary>
    public bool HasSeveralEditors => Editors.Count > 1;

    /// <summary>The model transfers go to. Null until one is picked (or the only one).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviewCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial BridgeEditor? SelectedEditor { get; set; }

    partial void OnSelectedEditorChanged(BridgeEditor? oldValue, BridgeEditor? newValue)
    {
        if (_bridgeClient is not null) _bridgeClient.Editor = newValue?.Id;
        EditorConnected = newValue is not null;
        BridgeStatusText = newValue is null
            ? (Editors.Count == 0 ? "Calculus is running — no model is open" : "Pick the model to transfer with")
            : $"Connected to {newValue}";
        // Every refresh hands over a fresh record of the same editor; a preview is
        // about one model, and only a different model makes it stale.
        if (oldValue?.Id != newValue?.Id)
        {
            ToCalculus.ClearPreview();
            ToTekla.ClearPreview();
        }
    }

    [ObservableProperty]
    public partial string BridgeStatusText { get; set; } = "Not connected";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviewCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial bool IsTransferring { get; set; } = false;

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

            var scope = ExportScope;
            Log.Information("Exporting {Scope} objects", scope);
            IsExporting = true;

            // Store the current work plane to restore it after export
            var workPlaneHandler = teklaModel.GetWorkPlaneHandler();
            var currentPlane = workPlaneHandler.GetCurrentTransformationPlane();
            try
            {
                var selected = TransferSource.TeklaObjects(teklaModel, scope);
                if (selected.Count == 0)
                {
                    throw new Exception(scope == TransferScope.Selected
                        ? "No objects selected for export"
                        : "The Tekla model has nothing to export");
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

    // --- Direct (bridge) transfer: preview, then apply or cancel (https://docs.calculus.build/reference/tekla-plugin/sync-behavior) ---

    /// <summary>
    /// Run <paramref name="body"/> against Tekla and the connected editor on a
    /// worker thread, the work plane set per <paramref name="coordinateSystem"/>
    /// and restored after. Errors go to the snackbar; null means it did not run
    /// or failed.
    /// </summary>
    private async Task<T?> OnTekla<T>(
        CoordinateSystemType coordinateSystem,
        Func<CalculusBridgeClient, TSM.Model, Task<T>> body,
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
                var workPlaneHandler = teklaModel.GetWorkPlaneHandler();
                var currentPlane = workPlaneHandler.GetCurrentTransformationPlane();
                if (coordinateSystem == CoordinateSystemType.Global)
                {
                    workPlaneHandler.SetCurrentTransformationPlane(new TSM.TransformationPlane());
                }
                try
                {
                    return await body(client, teklaModel);
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

    /// <summary>
    /// Read the source and classify. With <paramref name="again"/> (an Apply
    /// after a preview) the objects that preview read are read again by id — the
    /// selection may have moved since, not least by selecting preview rows.
    /// </summary>
    private static async Task<(SyncPlan plan, PreviewSource source)> Classify(
        SyncDirection direction,
        CalculusBridgeClient client,
        TSM.Model teklaModel,
        TransferScope scope,
        CoordinateSystemType coordinateSystem,
        PreviewSource? again = null)
    {
        static List<string> Guids(List<TSM.ModelObject> objects) =>
            [.. objects.Select(o => o.Identifier.GUID.ToString())];
        var teklaObjects = again?.TeklaIds is { } teklaIds
            ? TransferSource.TeklaObjects(teklaModel, teklaIds)
            : TransferSource.TeklaObjects(teklaModel, scope);

        if (direction == SyncDirection.TeklaToCalculus)
        {
            var plan = await BridgeTransferService.ClassifyToCalculusAsync(client, teklaObjects, teklaModel);
            if (plan.Items.Count == 0)
            {
                throw new InvalidOperationException(
                    plan.NotTransferable > 0 ? "No transferable objects in Tekla"
                    : scope == TransferScope.Selected ? "No objects selected in Tekla"
                    : "The Tekla model has nothing to transfer");
            }
            return (plan, new PreviewSource
            {
                Scope = scope,
                CoordinateSystem = coordinateSystem,
                TeklaIds = scope == TransferScope.Selected ? Guids(teklaObjects) : null,
            });
        }

        var calculusObjects = again?.CalculusIds is { } calculusIds
            ? await TransferSource.CalculusObjects(client, calculusIds)
            : await TransferSource.CalculusObjects(client, scope);
        var toTekla = await BridgeTransferService.ClassifyToTeklaAsync(client, calculusObjects, teklaObjects, teklaModel);
        if (toTekla.Items.Count == 0)
        {
            throw new InvalidOperationException(scope == TransferScope.Selected
                ? "Nothing transferable is selected in Calculus"
                : "The Calculus model has nothing to transfer");
        }
        return (toTekla, new PreviewSource
        {
            Scope = scope,
            CoordinateSystem = coordinateSystem,
            TeklaIds = scope == TransferScope.Selected ? Guids(teklaObjects) : null,
            CalculusIds = scope == TransferScope.Selected ? [.. calculusObjects.Select(o => o.GetId())] : null,
        });
    }

    private sealed class PreviewOutcome(SyncPlan plan, PreviewSource source, bool coloured, SyncResult? result = null)
    {
        public SyncPlan Plan { get; } = plan;
        public PreviewSource Source { get; } = source;
        public bool Coloured { get; } = coloured;
        /// <summary>Set when Apply wrote; null when it found the models changed and previewed again.</summary>
        public SyncResult? Result { get; } = result;
    }

    private static string PreviewText(SyncPlan plan, bool coloured) =>
        Capitalize(plan.Summary()) + (coloured
            ? ". Highlighted in both models; select rows to select them there."
            : $". Too many objects to highlight (over {SyncPreview.MaxColoured}); the list has them all.");

    private static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

    private TransferDirectionViewModel Other(TransferDirectionViewModel d) => d == ToCalculus ? ToTekla : ToCalculus;

    /// <summary>Classify and show what Apply would do, in both models and in the card's table. Nothing is written.</summary>
    [RelayCommand(CanExecute = nameof(CanTransfer))]
    async Task Preview(TransferDirectionViewModel d)
    {
        // The models show one preview at a time.
        Other(d).ClearPreview();
        var scope = d.Scope;
        var coordinateSystem = d.CoordinateSystem;
        var options = d.Options;
        var outcome = await OnTekla(coordinateSystem, async (client, teklaModel) =>
        {
            var (plan, source) = await Classify(d.Direction, client, teklaModel, scope, coordinateSystem);
            plan.ApplyDefaults(options);
            return new PreviewOutcome(plan, source, await SyncPreview.ShowAsync(client, plan));
        }, "Preview failed");
        if (outcome is null) return;
        d.ShowPreview(outcome.Plan, outcome.Source, PreviewText(outcome.Plan, outcome.Coloured));
    }

    /// <summary>
    /// Write the previewed changes that are checked. The source is classified
    /// again first: when the result differs from the preview (either model
    /// changed meanwhile), nothing is written and the card previews the new
    /// state instead, keeping the user's choices for the changes that stayed.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanApply))]
    async Task Apply(TransferDirectionViewModel d)
    {
        if (d.Plan is not { } preview || d.Source is not { } source) return;
        var previewKeys = preview.ChangeKeys();
        var included = new HashSet<string>(preview.Items.Where(i => i.Include).Select(i => i.Key));
        var options = d.Options;
        var outcome = await OnTekla(source.CoordinateSystem, async (client, teklaModel) =>
        {
            var (plan, again) = await Classify(d.Direction, client, teklaModel, source.Scope, source.CoordinateSystem, source);
            var same = plan.ChangeKeys().SetEquals(previewKeys);
            plan.ApplyDefaults(options);
            foreach (var item in plan.Items)
            {
                if (previewKeys.Contains(item.Key)) item.Include = included.Contains(item.Key);
            }
            if (!same)
            {
                return new PreviewOutcome(plan, again, await SyncPreview.ShowAsync(client, plan));
            }
            await SyncPreview.ClearAsync(client);
            var result = d.ToCalculus
                ? await BridgeTransferService.ApplyToCalculusAsync(client, plan, teklaModel)
                : await BridgeTransferService.ApplyToTeklaAsync(client, plan, teklaModel);
            return new PreviewOutcome(plan, again, false, result);
        }, "Transfer failed");
        if (outcome is null) return;

        if (outcome.Result is null)
        {
            d.ShowPreview(outcome.Plan, outcome.Source,
                "The models changed since the preview — check the list and apply again. " + PreviewText(outcome.Plan, outcome.Coloured));
            _snackbar.ShowWarningMessage("The models changed since the preview: nothing was written");
            return;
        }
        d.ClearPreview();
        var message = $"{(d.ToCalculus ? "To Calculus" : "To Tekla")}: {outcome.Result}";
        _snackbar.ShowSuccessMessage(message);
        TSM.Operations.Operation.DisplayPrompt($"Calculus Link: {message}");
    }

    /// <summary>Drop the preview: the colours leave both models, nothing is written.</summary>
    [RelayCommand]
    async Task CancelPreview(TransferDirectionViewModel d)
    {
        d.ClearPreview();
        if (_bridgeClient is null) return;
        try { await Task.Run(() => SyncPreview.ClearAsync(_bridgeClient)); }
        catch (Exception ex) { Log.Warning(ex, "Clearing the preview failed"); }
    }

    [RelayCommand]
    void IncludeSelected(TransferDirectionViewModel d) => d.SetIncluded(d.SelectedRows, true);

    [RelayCommand]
    void ExcludeSelected(TransferDirectionViewModel d) => d.SetIncluded(d.SelectedRows, false);

    bool CanTransfer(TransferDirectionViewModel? d) => BridgeRunning && EditorConnected && !IsTransferring;
    bool CanApply(TransferDirectionViewModel? d) => CanTransfer(d) && d?.HasPreview == true;

    // --- Preview rows selected -> the same objects selected in both models -------

    private CancellationTokenSource? _selectRows;

    /// <summary>
    /// Select the rows' objects in Tekla and Calculus — where each one exists: a
    /// new object only on its source side, a gone twin only on its target side.
    /// Debounced, so dragging over rows asks the models once.
    /// </summary>
    private async void OnPreviewRowsSelected(TransferDirectionViewModel d)
    {
        _selectRows?.Cancel();
        var cts = _selectRows = new CancellationTokenSource();
        try { await Task.Delay(150, cts.Token); }
        catch (TaskCanceledException) { return; }
        if (_bridgeClient is not { } client || IsTransferring || !d.HasPreview) return;

        var rows = d.SelectedRows;
        var teklaObjects = new ArrayList(rows.Select(r => r.Item.TeklaObject).OfType<TSM.ModelObject>().ToList());
        var calculusIds = rows
            .Where(r => d.ToCalculus ? r.State != SyncState.New : r.State != SyncState.Gone)
            .Select(r => r.CalculusId)
            .Distinct()
            .ToList();
        try
        {
            await Task.Run(async () =>
            {
                new TSUI.ModelObjectSelector().Select(teklaObjects);
                await client.SetSelectionAsync(calculusIds);
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Selecting preview rows in the models failed");
        }
    }
}
