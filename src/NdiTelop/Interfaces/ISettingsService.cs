using NdiTelop.Models;

namespace NdiTelop.Interfaces;
public interface ISettingsService
{
    AppSettings Settings { get; }
    Task LoadAsync();
    Task SaveAsync();
    Task ExportAsync(string filePath);
    Task ImportAsync(string filePath);
}
