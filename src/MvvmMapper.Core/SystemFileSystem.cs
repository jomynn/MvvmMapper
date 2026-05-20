namespace MvvmMapper.Core;

public sealed class SystemFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public string ReadAllText(string path) => File.ReadAllText(path);
    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern, System.IO.SearchOption searchOption)
        => Directory.EnumerateFiles(directory, searchPattern, searchOption);
    public string GetFileName(string path) => Path.GetFileName(path);
    public string GetFileNameWithoutExtension(string path) => Path.GetFileNameWithoutExtension(path);
    public string GetDirectoryName(string path) => Path.GetDirectoryName(path) ?? string.Empty;
    public string GetExtension(string path) => Path.GetExtension(path);
    public string CombinePath(params string[] paths) => Path.Combine(paths);
}
