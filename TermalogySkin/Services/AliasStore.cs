using System.Text.Json;
using System.IO;

namespace TermalogySkin.Services;

public sealed class AliasStore
{
    private static readonly JsonSerializerOptions WriteJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _path;

    public AliasStore(string appFolderName, string fileName)
    {
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localData, appFolderName);
        Directory.CreateDirectory(appFolder);
        _path = Path.Combine(appFolder, fileName);
    }

    public Dictionary<string, string> Load()
    {
        if (!File.Exists(_path))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = File.ReadAllText(_path);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return data is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public void Save(IReadOnlyDictionary<string, string> aliases)
    {
        var payload = new Dictionary<string, string>(aliases, StringComparer.OrdinalIgnoreCase);
        var json = JsonSerializer.Serialize(payload, WriteJsonOptions);

        File.WriteAllText(_path, json);
    }

    public string PathOnDisk => _path;
}
