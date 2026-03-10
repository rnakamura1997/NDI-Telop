using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiTelop.Models;
using NdiTelop.Services;
using NdiTelop.Interfaces;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace NdiTelop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly RenderService _renderService;
    private readonly IPresetService _presetService;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private Preset? _selectedPreset;

    public IReadOnlyList<Preset> Presets => _presetService.Presets;

    public MainWindowViewModel(RenderService renderService, IPresetService presetService)
    {
        _renderService = renderService;
        _presetService = presetService;
    }

    [RelayCommand]
    public async Task LoadPresetsAsync()
    {
        await _presetService.LoadPresetsAsync();
        SelectedPreset = Presets.FirstOrDefault();
        Status = $"Loaded {Presets.Count} presets.";
    }

    [RelayCommand]
    public void RenderPreview()
    {
        if (SelectedPreset == null)
        {
            Status = "No preset selected for preview.";
            return;
        }

        try
        {
            // In Phase 1a, we just render to a bitmap, not send to NDI yet.
            // The actual preview display will be handled by the UI.
            using var bitmap = _renderService.Render(SelectedPreset, 1280, 720);
            // For now, we don't display the bitmap directly in the ViewModel.
            // The UI will bind to SelectedPreset and use RenderService to draw.
            Status = $"Preview rendered for: {SelectedPreset.Name}";
        }
        catch (Exception ex)
        {
            Status = $"Error rendering preview: {ex.Message}";
        }
    }

    // Placeholder for future SavePreset command
    [RelayCommand]
    public async Task SaveSelectedPresetAsync()
    {
        if (SelectedPreset != null)
        {
            await _presetService.SavePresetAsync(SelectedPreset);
            Status = $"Preset saved: {SelectedPreset.Name}";
            // Presetsコレクションが更新されたことをUIに通知する必要がある場合、
            // _presetService.PresetsがObservableCollectionであれば自動的に通知されるはずです。
            // そうでなければ、手動で更新をトリガーする必要があります。
        }
        else
        {
            Status = "No preset selected to save.";
        }
    }

    // Placeholder for future DeletePreset command
    [RelayCommand]
    public async Task DeleteSelectedPresetAsync()
    {
        if (SelectedPreset != null)
        {
            var presetToDelete = SelectedPreset;
            SelectedPreset = null; // Clear selection before deleting
            await _presetService.DeletePresetAsync(presetToDelete.Id);
            Status = $"Preset deleted: {presetToDelete.Name}";
            // Presetsコレクションが更新されたことをUIに通知する必要がある場合、
            // _presetService.PresetsがObservableCollectionであれば自動的に通知されるはずです。
            // そうでなければ、手動で更新をトリガーする必要があります。
        }
        else
        {
            Status = "No preset selected to delete.";
        }
    }
}
