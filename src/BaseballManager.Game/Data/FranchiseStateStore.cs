using System.Text.Json;

namespace BaseballManager.Game.Data;

public sealed class FranchiseStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;
    private readonly object _fileLock = new();

    public FranchiseStateStore()
    {
        var repositoryRoot = RepositoryPathResolver.GetRepositoryRoot(AppContext.BaseDirectory);
        _filePath = Path.Combine(repositoryRoot, "data", "saves", "current-franchise.json");
    }

    public FranchiseSaveState Load()
    {
        if (!File.Exists(_filePath))
        {
            return new FranchiseSaveState();
        }

        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new FranchiseSaveState();
        }

        try
        {
            return JsonSerializer.Deserialize<FranchiseSaveState>(json, JsonOptions) ?? new FranchiseSaveState();
        }
        catch (JsonException)
        {
            return new FranchiseSaveState();
        }
    }

    public void Save(FranchiseSaveState saveState)
    {
        lock (_fileLock)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(saveState, JsonOptions);
            var tempFilePath = _filePath + ".tmp";
            File.WriteAllText(tempFilePath, json);

            if (File.Exists(_filePath))
            {
                File.Copy(tempFilePath, _filePath, overwrite: true);
                File.Delete(tempFilePath);
                return;
            }

            File.Move(tempFilePath, _filePath);
        }
    }
}
