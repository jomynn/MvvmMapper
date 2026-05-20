using MvvmMapper.Core;

namespace MvvmMapper.Core.Tests;

internal sealed class FakeFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _files;

    public FakeFileSystem(Dictionary<string, string> files)
    {
        _files = new Dictionary<string, string>(files, StringComparer.OrdinalIgnoreCase);
    }

    public bool FileExists(string path) => _files.ContainsKey(path);

    public bool DirectoryExists(string path) =>
        _files.Keys.Any(k => k.StartsWith(path, StringComparison.OrdinalIgnoreCase));

    public string ReadAllText(string path) =>
        _files.TryGetValue(path, out var content) ? content : throw new FileNotFoundException(path);

    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern, System.IO.SearchOption searchOption)
    {
        var ext = searchPattern.TrimStart('*');
        return _files.Keys.Where(k =>
            k.StartsWith(directory, StringComparison.OrdinalIgnoreCase) &&
            k.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    public string GetFileName(string path) => Path.GetFileName(path);
    public string GetFileNameWithoutExtension(string path) => Path.GetFileNameWithoutExtension(path);
    public string GetDirectoryName(string path) => Path.GetDirectoryName(path) ?? string.Empty;
    public string GetExtension(string path) => Path.GetExtension(path);
    public string CombinePath(params string[] paths) => Path.Combine(paths);
}
