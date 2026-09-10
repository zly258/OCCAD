using Avalonia.Controls;
using Avalonia.Platform.Storage;
using OCCAD;

namespace OCCAD.Avalonia;

public sealed partial class MainWindow
{
    private static readonly FilePickerFileType CadFileType =
        new("OCCAD")
        {
            Patterns = ["*.ocad"]
        };

    private static readonly FilePickerFileType JsonFileType =
        new("JSON")
        {
            Patterns = ["*.json"]
        };

    private async Task NewDocumentAsync()
    {
        if (!await ConfirmSaveChangesAsync())
            return;

        if (!_workspace.Actions.Execute("file.new"))
            return;

        _documentFile = null;
        ResetDocumentUi(fit: false);
        UpdateWindowTitle();
    }

    private void ClearModel()
    {
        if (!_workspace.Actions.Execute("file.clear"))
            return;

        ResetDocumentUi(fit: false);
    }

    private async Task OpenDocumentAsync()
    {
        if (!StorageProvider.CanOpen)
            return;

        if (!await ConfirmSaveChangesAsync())
            return;

        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = UiText(
                    "Cad.Text.OpenDocumentTitle",
                    "Open CAD Document"),
                AllowMultiple = false,
                FileTypeFilter =
                    [
                        CadFileType,
                        JsonFileType,
                        FilePickerFileTypes.All
                    ]
            });

        if (files.Count == 0)
            return;

        var file = files[0];

        try
        {
            await using var stream =
                await file.OpenReadAsync();

            CadDocumentSerializer.Load(
                _workspace,
                stream);

            _documentFile = file;
            ResetDocumentUi(fit: true);
            UpdateWindowTitle();
            _toolStatus.Text = UiFormat(
                "Cad.Text.Opened",
                "Opened {0}",
                file.Name);
        }
        catch (Exception exception)
        {
            _toolStatus.Text = exception.Message;
            await CadMessageDialog.ShowAsync(
                this,
                UiText(
                    "Cad.Text.OpenDocumentTitle",
                    "Open CAD Document"),
                exception.Message);
        }
    }

    private async Task<bool> SaveDocumentAsync(
        bool saveAs)
    {
        var file = _documentFile;

        if (saveAs || file is null)
        {
            if (!StorageProvider.CanSave)
                return false;

            file = await StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = UiText(
                        "Cad.Text.SaveDocumentTitle",
                        "Save CAD Document"),
                    DefaultExtension = "ocad",
                    SuggestedFileName =
                        file?.Name ??
                        $"{UiText("Cad.Text.Drawing", "Drawing")}.ocad",
                    ShowOverwritePrompt = true,
                    FileTypeChoices =
                        [
                            CadFileType,
                            JsonFileType
                        ]
                });

            if (file is null)
                return false;
        }

        try
        {
            await using var stream =
                await file.OpenWriteAsync();

            if (stream.CanSeek)
            {
                stream.Position = 0;
                stream.SetLength(0);
            }

            CadDocumentSerializer.Save(
                _workspace,
                stream);
            await stream.FlushAsync();

            _documentFile = file;
            _workspace.MarkSaved();
            UpdateWindowTitle();

            _toolStatus.Text = UiFormat(
                "Cad.Text.Saved",
                "Saved {0}",
                file.Name);
            return true;
        }
        catch (Exception exception)
        {
            _toolStatus.Text = exception.Message;
            await CadMessageDialog.ShowAsync(
                this,
                UiText(
                    "Cad.Text.SaveDocumentTitle",
                    "Save CAD Document"),
                exception.Message);
            return false;
        }
    }

    private void ResetDocumentUi(bool fit)
    {
        _propertyInspector.InspectEntities([]);
        RefreshTree();
        RefreshLayerUi();
        _layerPanel.Refresh();
        RefreshInteractionUi();
        UpdateSelectionStatus();
        UpdateHistoryUi();

        if (fit)
            _workspace.Actions.Execute("view.fit");
    }

    private async Task<bool> ConfirmSaveChangesAsync()
    {
        if (!_workspace.IsModified)
            return true;

        var documentName =
            _documentFile?.Name ??
            UiText(
                "Cad.Text.Drawing",
                "Drawing");

        var result = await CadMessageDialog.ShowAsync(
            this,
            UiText(
                "Cad.Text.UnsavedTitle",
                "Unsaved Changes"),
            UiFormat(
                "Cad.Text.UnsavedMessage",
                "Save changes to {0}?",
                documentName),
            yesNoCancel: true);

        return result switch
        {
            CadDialogResult.Yes =>
                await SaveDocumentAsync(saveAs: false),
            CadDialogResult.No => true,
            _ => false
        };
    }

    protected override void OnClosing(
        WindowClosingEventArgs e)
    {
        if (_closingConfirmed)
        {
            DisposeWorkspace();
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        _ = ConfirmAndCloseAsync();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        DisposeWorkspace();
        base.OnClosed(e);
    }

    private async Task ConfirmAndCloseAsync()
    {
        if (!await ConfirmSaveChangesAsync())
            return;

        _closingConfirmed = true;
        Close();
    }

    private void UpdateWindowTitle()
    {
        var name =
            _documentFile?.Name ??
            UiText(
                "Cad.Text.Drawing",
                "Drawing");
        var modified =
            _workspace.IsModified ? " *" : string.Empty;
        Title = $"OCCAD - {name}{modified}";
    }
}
