namespace BaseballManager.Game.Data;

internal static class RepositoryPathResolver
{
    public static string GetRepositoryRoot(string startPath)
    {
        var current = new DirectoryInfo(startPath);

        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "BaseballManager.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}