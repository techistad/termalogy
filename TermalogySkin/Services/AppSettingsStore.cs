using System.IO;
using System.Text.Json;

namespace TermalogySkin.Services;

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions WriteJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _path;

    public AppSettingsStore(string appFolderName, string fileName)
    {
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localData, appFolderName);
        Directory.CreateDirectory(appFolder);
        _path = Path.Combine(appFolder, fileName);
    }

    public AppSettings Load()
    {
        if (!File.Exists(_path))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, WriteJsonOptions);
        File.WriteAllText(_path, json);
    }

    public string PathOnDisk => _path;
}

public sealed class AppSettings
{
    public bool StealthMode { get; set; }
}
