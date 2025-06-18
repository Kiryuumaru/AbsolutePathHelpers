using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

#if NETSTANDARD
#elif NET5_0_OR_GREATER
using static AbsolutePathHelpers.Common.Internals.Message;
#endif

namespace AbsolutePathHelpers;

public static partial class AbsolutePathExtensions
{
    /// <summary>
    /// Converts the <see cref="AbsolutePath"/> to a <see cref="FileInfo"/> object.
    /// </summary>
    /// <param name="absolutePath">The absolute path to convert.</param>
    /// <returns>A <see cref="FileInfo"/> object representing the file, or null if the path is null.</returns>
    public static FileInfo? ToFileInfo(this AbsolutePath absolutePath)
    {
        return absolutePath.Path is not null ? new FileInfo(absolutePath.Path) : null;
    }

    /// <summary>
    /// Converts the <see cref="AbsolutePath"/> to a <see cref="DirectoryInfo"/> object.
    /// </summary>
    /// <param name="absolutePath">The absolute path to convert.</param>
    /// <returns>A <see cref="DirectoryInfo"/> object representing the directory, or null if the path is null.</returns>
    public static DirectoryInfo? ToDirectoryInfo(this AbsolutePath absolutePath)
    {
        return absolutePath.Path is not null ? new DirectoryInfo(absolutePath.Path) : null;
    }

    /// <summary>
    /// Checks if the path specified by the <see cref="AbsolutePath"/> exists as either a file or directory.
    /// </summary>
    /// <param name="absolutePath">The absolute path to check for existence.</param>
    /// <returns><c>true</c> if the path exists as either a file or directory; otherwise, <c>false</c>.</returns>
    public static bool IsExists(this AbsolutePath absolutePath)
    {
        return Directory.Exists(absolutePath.Path) || File.Exists(absolutePath.Path);
    }

    /// <summary>
    /// Checks if the path specified by the <see cref="AbsolutePath"/> exists as a file.
    /// </summary>
    /// <param name="absolutePath">The absolute path to check for file existence.</param>
    /// <returns><c>true</c> if the path exists as a file; otherwise, <c>false</c>.</returns>
    public static bool FileExists(this AbsolutePath absolutePath)
    {
        return File.Exists(absolutePath.Path);
    }

    /// <summary>
    /// Checks if the path specified by the <see cref="AbsolutePath"/> exists as a directory.
    /// </summary>
    /// <param name="absolutePath">The absolute path to check for directory existence.</param>
    /// <returns><c>true</c> if the path exists as a directory; otherwise, <c>false</c>.</returns>
    public static bool DirectoryExists(this AbsolutePath absolutePath)
    {
        return Directory.Exists(absolutePath.Path);
    }

    /// <summary>
    /// Determines whether the directory specified by the <see cref="AbsolutePath"/> contains any files matching the specified pattern.
    /// </summary>
    /// <param name="absolutePath">The absolute path of the directory to search in.</param>
    /// <param name="pattern">The search pattern to match against file names (e.g., "*.txt").</param>
    /// <param name="options">Specifies whether to search only the current directory or include all subdirectories.</param>
    /// <returns><c>true</c> if the directory contains at least one file matching the pattern; otherwise, <c>false</c>.</returns>
    /// <remarks>Returns <c>false</c> if the directory does not exist.</remarks>
    public static bool ContainsFile(this AbsolutePath absolutePath, string pattern, SearchOption options = SearchOption.TopDirectoryOnly)
    {
        return ToDirectoryInfo(absolutePath)?.GetFiles(pattern, options).Length != 0;
    }

    /// <summary>
    /// Determines whether the directory specified by the <see cref="AbsolutePath"/> contains any subdirectories matching the specified pattern.
    /// </summary>
    /// <param name="absolutePath">The absolute path of the directory to search in.</param>
    /// <param name="pattern">The search pattern to match against directory names (e.g., "bin*").</param>
    /// <param name="options">Specifies whether to search only the current directory or include all subdirectories.</param>
    /// <returns><c>true</c> if the directory contains at least one subdirectory matching the pattern; otherwise, <c>false</c>.</returns>
    /// <remarks>Returns <c>false</c> if the directory does not exist.</remarks>
    public static bool ContainsDirectory(this AbsolutePath absolutePath, string pattern, SearchOption options = SearchOption.TopDirectoryOnly)
    {
        return ToDirectoryInfo(absolutePath)?.GetDirectories(pattern, options).Length != 0;
    }

    /// <summary>
    /// Retrieves all files within the directory specified by the <see cref="AbsolutePath"/> that match the given criteria.
    /// </summary>
    /// <param name="absolutePath">The absolute path of the directory to search in.</param>
    /// <param name="pattern">The search pattern to match against file names (e.g., "*.txt"). Default is "*" (all files).</param>
    /// <param name="depth">The maximum number of directory levels to search. Default is 1 (current directory only).</param>
    /// <param name="attributes">Optional file attributes that files must have to be included. Default is 0 (no attribute filtering).</param>
    /// <returns>An enumerable collection of <see cref="AbsolutePath"/> objects representing the matching files.</returns>
    /// <remarks>Returns an empty collection if the directory does not exist or if depth is 0.</remarks>
    public static IEnumerable<AbsolutePath> GetFiles(
        this AbsolutePath absolutePath,
        string pattern = "*",
        int depth = 1,
        FileAttributes attributes = 0)
    {
        if (!DirectoryExists(absolutePath)) return [];

        if (depth == 0)
            return [];

        var files = Directory.EnumerateFiles(absolutePath.Path, pattern, SearchOption.TopDirectoryOnly)
            .Where(x => (File.GetAttributes(x) & attributes) == attributes)
            .OrderBy(x => x)
            .Select(AbsolutePath.Create);

        return files.Concat(GetDirectories(absolutePath, depth: depth - 1).SelectMany(x => x.GetFiles(pattern, attributes: attributes)));
    }

    /// <summary>
    /// Retrieves all directories within the directory specified by the <see cref="AbsolutePath"/> that match the given criteria.
    /// </summary>
    /// <param name="absolutePath">The absolute path of the directory to search in.</param>
    /// <param name="pattern">The search pattern to match against directory names (e.g., "bin*"). Default is "*" (all directories).</param>
    /// <param name="depth">The maximum number of directory levels to search. Default is 1 (current directory only).</param>
    /// <param name="attributes">Optional directory attributes that directories must have to be included. Default is 0 (no attribute filtering).</param>
    /// <returns>An enumerable collection of <see cref="AbsolutePath"/> objects representing the matching directories.</returns>
    /// <remarks>Returns an empty collection if the directory does not exist or if depth is 0.</remarks>
    public static IEnumerable<AbsolutePath> GetDirectories(
        this AbsolutePath absolutePath,
        string pattern = "*",
        int depth = 1,
        FileAttributes attributes = 0)
    {
        if (DirectoryExists(absolutePath))
        {
            var paths = new string[] { absolutePath.Path };
            while (paths.Length != 0 && depth > 0)
            {
                var matchingDirectories = paths
                    .SelectMany(x => Directory.EnumerateDirectories(x, pattern, SearchOption.TopDirectoryOnly))
                    .Where(x => (File.GetAttributes(x) & attributes) == attributes)
                    .OrderBy(x => x)
                    .Select(AbsolutePath.Create).ToList();

                foreach (var matchingDirectory in matchingDirectories)
                    yield return matchingDirectory;

                depth--;
                paths = [.. paths.SelectMany(x => Directory.GetDirectories(x, "*", SearchOption.TopDirectoryOnly))];
            }
        }
    }

    /// <summary>
    /// Retrieves all files and directories within the directory specified by the <see cref="AbsolutePath"/>.
    /// </summary>
    /// <param name="absolutePath">The absolute path of the directory to search in.</param>
    /// <returns>An enumerable collection of <see cref="AbsolutePath"/> objects representing all files and directories.</returns>
    /// <remarks>Returns an empty collection if the directory does not exist.</remarks>
    public static IEnumerable<AbsolutePath> GetPaths(this AbsolutePath absolutePath)
    {
        var paths = new List<AbsolutePath>();
        paths.AddRange(GetFiles(absolutePath));
        paths.AddRange(GetDirectories(absolutePath));
        return paths;
    }

    /// <summary>
    /// Asynchronously reads all text from the file specified by the <see cref="AbsolutePath"/>.
    /// </summary>
    /// <param name="absolutePath">The absolute path of the file to read.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous read operation. The result contains the file content as a string.</returns>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="IOException">An I/O error occurs while opening the file.</exception>
    public static Task<string> ReadAllText(this AbsolutePath absolutePath, CancellationToken cancellationToken = default)
    {
        return File.ReadAllTextAsync(absolutePath.Path, cancellationToken);
    }

    /// <summary>
    /// Determines whether the current path is a parent directory of the specified child path.
    /// </summary>
    /// <param name="parent">The potential parent path to check.</param>
    /// <param name="child">The potential child path to compare with.</param>
    /// <returns><c>true</c> if the current path is a parent directory of the child path; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This method checks if the child path is located within the parent directory or any of its subdirectories.
    /// The method returns <c>false</c> if the paths are the same.
    /// </remarks>
    public static bool IsParentOf(this AbsolutePath parent, AbsolutePath child)
    {
        var pathToCheck = child.Parent;

        while (pathToCheck != null)
        {
            if (pathToCheck == parent)
            {
                return true;
            }

            pathToCheck = pathToCheck.Parent;
        }

        return false;
    }

    /// <summary>
    /// Determines whether the current path is the same as or a parent directory of the specified child path.
    /// </summary>
    /// <param name="parent">The potential parent path to check.</param>
    /// <param name="child">The potential child path to compare with.</param>
    /// <returns><c>true</c> if the current path is the same as or a parent directory of the child path; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This method checks if the paths are equal or if the child path is located within the parent directory or any of its subdirectories.
    /// </remarks>
    public static bool IsParentOrSelfOf(this AbsolutePath parent, AbsolutePath child)
    {
        if (parent == child)
        {
            return true;
        }

        return IsParentOf(parent, child);
    }

    /// <summary>
    /// Determines whether the current path is a child directory of the specified parent path.
    /// </summary>
    /// <param name="child">The potential child path to check.</param>
    /// <param name="parent">The potential parent path to compare with.</param>
    /// <returns><c>true</c> if the current path is a child directory of the parent path; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This method checks if the child path is located within the parent directory or any of its subdirectories.
    /// The method returns <c>false</c> if the paths are the same.
    /// </remarks>
    public static bool IsChildOf(this AbsolutePath child, AbsolutePath parent)
    {
        return IsParentOf(parent, child);
    }

    /// <summary>
    /// Determines whether the current path is the same as or a child directory of the specified parent path.
    /// </summary>
    /// <param name="child">The potential child path to check.</param>
    /// <param name="parent">The potential parent path to compare with.</param>
    /// <returns><c>true</c> if the current path is the same as or a child directory of the parent path; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This method checks if the paths are equal or if the child path is located within the parent directory or any of its subdirectories.
    /// </remarks>
    public static bool IsChildOrSelfOf(this AbsolutePath child, AbsolutePath parent)
    {
        return IsParentOrSelfOf(parent, child);
    }

    /// <summary>
    /// Creates a map of all files, folders, and symbolic links in the directory structure starting from the specified path.
    /// </summary>
    /// <param name="path">The starting path to map.</param>
    /// <returns>A <see cref="FileMap"/> containing information about files, folders, and symbolic links.</returns>
    private static FileMap GetFileMap(AbsolutePath path)
    {
        List<AbsolutePath> files = [];
        List<AbsolutePath> folders = [];
        List<(AbsolutePath Link, AbsolutePath Target)> symbolicLinks = [];

        List<AbsolutePath> next = [path];

        while (next.Count != 0)
        {
            List<AbsolutePath> forNext = [];

            foreach (var item in next)
            {
                bool hasNext = false;

                if (item.FileExists())
                {
                    FileInfo fileInfo = new(item);
                    if (fileInfo.LinkTarget != null)
                    {
                        string linkTarget;
                        if (!Path.IsPathRooted(fileInfo.LinkTarget))
                        {
                            linkTarget = item.Parent! / fileInfo.LinkTarget;
                        }
                        else
                        {
                            linkTarget = fileInfo.LinkTarget;
                        }
                        if (linkTarget.StartsWith("\\??\\"))
                        {
                            linkTarget = linkTarget[4..];
                        }
                        symbolicLinks.Add((item, linkTarget));
                    }
                    else
                    {
                        files.Add(item);
                    }
                }
                else if (item.DirectoryExists())
                {
                    DirectoryInfo directoryInfo = new(item);
                    if (directoryInfo.LinkTarget != null)
                    {
                        string linkTarget;
                        if (!Path.IsPathRooted(directoryInfo.LinkTarget))
                        {
                            linkTarget = item.Parent! / directoryInfo.LinkTarget;
                        }
                        else
                        {
                            linkTarget = directoryInfo.LinkTarget;
                        }
                        if (linkTarget.StartsWith("\\??\\"))
                        {
#if NETSTANDARD
                            linkTarget = linkTarget.Substring(4);
#elif NET5_0_OR_GREATER
                            linkTarget = linkTarget[4..];
#endif
                        }
                        symbolicLinks.Add((item, linkTarget));
                    }
                    else
                    {
                        folders.Add(item);

                        hasNext = true;
                    }
                }

                if (hasNext)
                {
                    try
                    {
                        forNext.AddRange(Directory.GetFiles(item, "*", SearchOption.TopDirectoryOnly).Select(AbsolutePath.Create));
                    }
                    catch { }
                    try
                    {
                        forNext.AddRange(Directory.GetDirectories(item, "*", SearchOption.TopDirectoryOnly).Select(AbsolutePath.Create));
                    }
                    catch { }
                }
            }

            next = forNext;
        }

        List<(AbsolutePath Link, AbsolutePath Target)> arangedSymbolicLinks = [];
        foreach (var symbolicLink in symbolicLinks)
        {
            bool add = true;
            foreach (var arangedSymbolicLink in new List<(AbsolutePath Link, AbsolutePath Target)>(arangedSymbolicLinks))
            {
                if (symbolicLink.Target == arangedSymbolicLink.Link)
                {
                    arangedSymbolicLinks.Insert(arangedSymbolicLinks.IndexOf(arangedSymbolicLink) + 1, symbolicLink);
                    add = false;
                    break;
                }
                else if (symbolicLink.Link == arangedSymbolicLink.Target)
                {
                    arangedSymbolicLinks.Insert(arangedSymbolicLinks.IndexOf(arangedSymbolicLink), symbolicLink);
                    add = false;
                    break;
                }
            }
            if (add)
            {
                arangedSymbolicLinks.Add(symbolicLink);
            }
        }

        return new(path, [.. files], [.. folders], [.. arangedSymbolicLinks]);
    }
}

/// <summary>
/// Represents a hierarchical map of files, folders, and symbolic links in a directory structure.
/// </summary>
/// <param name="source">The source path that was mapped.</param>
/// <param name="files">The collection of file paths found.</param>
/// <param name="folders">The collection of folder paths found.</param>
/// <param name="symbolicLinks">The collection of symbolic links found, with their target paths.</param>
internal class FileMap(AbsolutePath source, AbsolutePath[] files, AbsolutePath[] folders, (AbsolutePath Link, AbsolutePath Target)[] symbolicLinks)
{
    /// <summary>
    /// Gets the source path that was mapped.
    /// </summary>
    public AbsolutePath Source { get; } = source;

    /// <summary>
    /// Gets the collection of file paths found.
    /// </summary>
    public AbsolutePath[] Files { get; } = files;

    /// <summary>
    /// Gets the collection of folder paths found.
    /// </summary>
    public AbsolutePath[] Folders { get; } = folders;

    /// <summary>
    /// Gets the collection of symbolic links found, with their target paths.
    /// </summary>
    public (AbsolutePath Link, AbsolutePath Target)[] SymbolicLinks { get; } = symbolicLinks;
}