using System.ComponentModel;
using System.Runtime.CompilerServices;
using LiteMCLauncher.Models;

namespace LiteMCLauncher.Services;

/// <summary>
/// Holds the instance selected by the user across Home, Instances and Mods.
/// Also owns the per-instance game directory layout.
/// </summary>
public sealed class LauncherState : INotifyPropertyChanged
{
    private MinecraftInstance? _selectedInstance;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MinecraftInstance? SelectedInstance
    {
        get => _selectedInstance;
        set
        {
            if (Equals(_selectedInstance, value))
            {
                return;
            }

            _selectedInstance = value;
            OnPropertyChanged();

            if (value is not null)
            {
                EnsureInstanceDirectories(value);
            }
        }
    }

    public string GetGameDirectory(MinecraftInstance instance)
    {
        var safeId = GetSafeDirectoryName(instance.Id);
        return Path.Combine(AppPaths.MinecraftDirectory, "instances", safeId);
    }

    public string GetModsDirectory(MinecraftInstance instance) =>
        Path.Combine(GetGameDirectory(instance), "mods");

    public string GetModpacksDirectory(MinecraftInstance instance) =>
        Path.Combine(GetGameDirectory(instance), "modpacks");

    public void EnsureInstanceDirectories(MinecraftInstance instance)
    {
        var gameDirectory = GetGameDirectory(instance);
        Directory.CreateDirectory(gameDirectory);

        foreach (var subdirectory in new[]
                 {
                     "mods",
                     "modpacks",
                     "config",
                     "saves",
                     "resourcepacks",
                     "shaderpacks",
                     "logs",
                     "crash-reports",
                 })
        {
            Directory.CreateDirectory(Path.Combine(gameDirectory, subdirectory));
        }
    }

    private static string GetSafeDirectoryName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeName = new string(name
            .Select(character => invalidChars.Contains(character) ? '_' : character)
            .ToArray())
            .Trim();

        return string.IsNullOrWhiteSpace(safeName) ? "instance" : safeName;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
