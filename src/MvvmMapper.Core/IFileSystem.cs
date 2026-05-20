namespace MvvmMapper.Core;

/// <summary>File system abstraction to keep Core testable without touching real disk.</summary>
public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    string ReadAllText(string path);
    IEnumerable<string> EnumerateFiles(string directory, string searchPattern, System.IO.SearchOption searchOption);
    string GetFileName(string path);
    string GetFileNameWithoutExtension(string path);
    string GetDirectoryName(string path);
    string GetExtension(string path);
    string CombinePath(params string[] paths);
}
