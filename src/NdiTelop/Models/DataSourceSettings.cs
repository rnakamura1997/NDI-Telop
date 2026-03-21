using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace NdiTelop.Models;

public partial class DataSourceSettings : ObservableObject
{
    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private string _source = string.Empty;

    [ObservableProperty]
    private int _refreshIntervalSeconds = 5;

    [ObservableProperty]
    private string _lastStatus = "Not configured";

    [ObservableProperty]
    private DateTimeOffset? _lastUpdatedUtc;

    [ObservableProperty]
    private ObservableCollection<DataSourceFieldValue> _fields = [];

    public IReadOnlyDictionary<string, string> AsDictionary()
        => Fields
            .GroupBy(field => field.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);
}

public partial class DataSourceFieldValue : ObservableObject
{
    [ObservableProperty]
    private string _key = string.Empty;

    [ObservableProperty]
    private string _value = string.Empty;
}
