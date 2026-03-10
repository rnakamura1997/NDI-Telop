using CommunityToolkit.Mvvm.ComponentModel;
using NdiTelop.Models;
using System.Collections.ObjectModel;

namespace NdiTelop.ViewModels;

public partial class PresetEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private Preset _currentPreset = new();

    public ObservableCollection<TextLine> TextLines { get; private set; } = new ObservableCollection<TextLine>();

    public PresetEditorViewModel()
    {
        // Initialize with a default preset or load from somewhere
        // For now, just ensure CurrentPreset is not null
    }

    partial void OnCurrentPresetChanged(Preset value)
    {
        TextLines.Clear();
        if (value?.TextLines != null)
        {
            foreach (var line in value.TextLines)
            {
                TextLines.Add(line);
            }
        }
    }

    public void SetPreset(Preset preset)
    {
        CurrentPreset = preset;
    }
}
