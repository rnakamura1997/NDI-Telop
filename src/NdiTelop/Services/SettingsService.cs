using NdiTelop.Interfaces;
using NdiTelop.Models;

namespace NdiTelop.Services;

public class SettingsService : ISettingsService
{
    public AppSettings Settings { get; } = new();

    public Task LoadAsync() => Task.CompletedTask;
    public Task SaveAsync() => Task.CompletedTask;
    public Task ExportAsync(string filePath) => Task.CompletedTask;
    public Task ImportAsync(string filePath) => Task.CompletedTask;
}
