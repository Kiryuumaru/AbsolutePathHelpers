using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace AbsolutePathHelpers;

/// <summary>
/// Represents an absolute file or directory path.
/// </summary>
/// <remarks>
/// Creates an instance of <see cref="AbsolutePath"/>.
/// </remarks>
/// <param name="path">The path to use for the <see cref="AbsolutePath"/>.</param>
public class AbsolutePath(string path) : IEquatable<AbsolutePath?>
{
    /// <summary>
    /// Creates a new instance of <see cref="AbsolutePath"/> from the specified path string.
    /// </summary>
    /// <param name="path">The path to use for the <see cref="AbsolutePath"/>.</param>
    /// <returns>A new instance of <see cref="AbsolutePath"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the path is null or empty.</exception>
    public static AbsolutePath Create(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty", nameof(path));
        }

        return new AbsolutePath(System.IO.Path.IsPathRooted(path)
            ? System.IO.Path.GetFullPath(path)
            : System.IO.Path.GetFullPath(System.IO.Path.Combine(Directory.GetCurrentDirectory(), path)));
    }

    /// <summary>
    /// Attempts to create a new instance of <see cref="AbsolutePath"/> from the specified path string.
    /// </summary>
    /// <param name="path">The path to use for the <see cref="AbsolutePath"/>.</param>
    /// <param name="absolutePath">
    /// When this method returns, contains the created <see cref="AbsolutePath"/> if the path is valid; otherwise, <c>null</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> if the <see cref="AbsolutePath"/> was created successfully; otherwise, <c>false</c>.
    /// </returns>
    public static bool TryCreate(string path, [NotNullWhen(true)] out AbsolutePath? absolutePath)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            absolutePath = null;
            return false;
        }
        
        try
        {
            absolutePath = new AbsolutePath(System.IO.Path.IsPathRooted(path)
                ? System.IO.Path.GetFullPath(path)
                : System.IO.Path.GetFullPath(System.IO.Path.Combine(Directory.GetCurrentDirectory(), path)));
            return true;
        }
        catch
        {
            absolutePath = null;
            return false;
        }
    }

    /// <summary>
    /// Gets the absolute path.
    /// </summary>
    public string Path { get; } = System.IO.Path.GetFullPath(path);

    /// <summary>
    /// Gets the parent directory of the current path.
    /// </summary>
    [JsonIgnore]
    public AbsolutePath? Parent
    {
        get
        {
            var parentDir = Directory.GetParent(Path);
            return parentDir == null ? null : Create(parentDir.ToString());
        }
    }

    /// <summary>
    /// Gets the file name without extension of the current path.
    /// </summary>
    [JsonIgnore]
    public string Stem
    {
        get => System.IO.Path.GetFileNameWithoutExtension(Path);
    }

    /// <summary>
    /// Gets the file name of the current path.
    /// </summary>
    [JsonIgnore]
    public string Name
    {
        get => System.IO.Path.GetFileName(Path);
    }

    /// <summary>
    /// Gets the file extension of the current path.
    /// </summary>
    [JsonIgnore]
    public string Extension
    {
        get => System.IO.Path.GetExtension(Path);
    }

    /// <summary>
    /// Combines the current path with a relative path.
    /// </summary>
    /// <param name="basePath">The base <see cref="AbsolutePath"/>.</param>
    /// <param name="relativePath">The relative path to combine with.</param>
    /// <returns>A new <see cref="AbsolutePath"/> representing the combined path.</returns>
    public static AbsolutePath operator /(AbsolutePath basePath, string relativePath)
    {
        return new AbsolutePath(System.IO.Path.Combine(basePath.Path, relativePath));
    }

    /// <summary>
    /// Combines the current path with multiple path segments.
    /// </summary>
    /// <param name="basePath">The base <see cref="AbsolutePath"/>.</param>
    /// <param name="pathSegments">The path segments to combine with.</param>
    /// <returns>A new <see cref="AbsolutePath"/> representing the combined path.</returns>
    public static AbsolutePath operator /(AbsolutePath basePath, IEnumerable<string> pathSegments)
    {
        AbsolutePath path = basePath;
        foreach (var segment in pathSegments)
        {
            path = new AbsolutePath(System.IO.Path.Combine(path.Path, segment));
        }
        return path;
    }

    /// <summary>
    /// Implicitly converts a string to an <see cref="AbsolutePath"/>.
    /// </summary>
    /// <param name="path">The string path to convert.</param>
    /// <returns>A new <see cref="AbsolutePath"/> instance.</returns>
    public static implicit operator AbsolutePath(string path) => new(path);

    /// <summary>
    /// Implicitly converts a string array to an <see cref="AbsolutePath"/>.
    /// </summary>
    /// <param name="pathSegments">The string path segments to combine and convert.</param>
    /// <returns>A new <see cref="AbsolutePath"/> instance.</returns>
    public static implicit operator AbsolutePath(string[] pathSegments) => new(System.IO.Path.Combine(pathSegments));

    /// <summary>
    /// Implicitly converts an <see cref="AbsolutePath"/> to a string.
    /// </summary>
    /// <param name="path">The <see cref="AbsolutePath"/> to convert.</param>
    /// <returns>The string representation of the path.</returns>
    public static implicit operator string(AbsolutePath path) => path.Path;

    /// <summary>
    /// Implicitly converts an <see cref="AbsolutePath"/> to a <see cref="FileInfo"/>.
    /// </summary>
    /// <param name="path">The <see cref="AbsolutePath"/> to convert.</param>
    /// <returns>A <see cref="FileInfo"/> instance for the path.</returns>
    public static implicit operator FileInfo?(AbsolutePath path) => path.ToFileInfo();

    /// <summary>
    /// Implicitly converts an <see cref="AbsolutePath"/> to a <see cref="DirectoryInfo"/>.
    /// </summary>
    /// <param name="path">The <see cref="AbsolutePath"/> to convert.</param>
    /// <returns>A <see cref="DirectoryInfo"/> instance for the path.</returns>
    public static implicit operator DirectoryInfo?(AbsolutePath path) => path.ToDirectoryInfo();

    /// <summary>
    /// Determines whether two <see cref="AbsolutePath"/> instances are equal.
    /// </summary>
    /// <param name="left">The first <see cref="AbsolutePath"/> to compare.</param>
    /// <param name="right">The second <see cref="AbsolutePath"/> to compare.</param>
    /// <returns><c>true</c> if the paths are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(AbsolutePath? left, AbsolutePath? right)
    {
        return EqualityComparer<AbsolutePath>.Default.Equals(left, right);
    }

    /// <summary>
    /// Determines whether two <see cref="AbsolutePath"/> instances are not equal.
    /// </summary>
    /// <param name="left">The first <see cref="AbsolutePath"/> to compare.</param>
    /// <param name="right">The second <see cref="AbsolutePath"/> to compare.</param>
    /// <returns><c>true</c> if the paths are not equal; otherwise, <c>false</c>.</returns>
    public static bool operator !=(AbsolutePath? left, AbsolutePath? right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Returns the string representation of the absolute path.
    /// </summary>
    /// <returns>The absolute path as a string.</returns>
    public override string ToString()
    {
        return Path;
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current <see cref="AbsolutePath"/>.
    /// </summary>
    /// <param name="obj">The object to compare with the current <see cref="AbsolutePath"/>.</param>
    /// <returns><c>true</c> if the specified object is equal to the current <see cref="AbsolutePath"/>; otherwise, <c>false</c>.</returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as AbsolutePath);
    }

    /// <summary>
    /// Determines whether the specified <see cref="AbsolutePath"/> is equal to the current <see cref="AbsolutePath"/>.
    /// </summary>
    /// <param name="other">The <see cref="AbsolutePath"/> to compare with the current <see cref="AbsolutePath"/>.</param>
    /// <returns><c>true</c> if the specified <see cref="AbsolutePath"/> is equal to the current <see cref="AbsolutePath"/>; otherwise, <c>false</c>.</returns>
    public bool Equals(AbsolutePath? other)
    {
        return
            other is not null &&
            System.IO.Path.GetFullPath(Path).Equals(System.IO.Path.GetFullPath(other.Path), StringComparison.CurrentCultureIgnoreCase);
    }

    /// <summary>
    /// Serves as the default hash function.
    /// </summary>
    /// <returns>A hash code for the current <see cref="AbsolutePath"/>.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(Path);
    }

    /// <summary>
    /// Converts this <see cref="AbsolutePath"/> to a <see cref="FileInfo"/> instance.
    /// </summary>
    /// <returns>A <see cref="FileInfo"/> instance for this path.</returns>
    public FileInfo? ToFileInfo()
    {
        return new FileInfo(Path);
    }

    /// <summary>
    /// Converts this <see cref="AbsolutePath"/> to a <see cref="DirectoryInfo"/> instance.
    /// </summary>
    /// <returns>A <see cref="DirectoryInfo"/> instance for this path.</returns>
    public DirectoryInfo? ToDirectoryInfo()
    {
        return new DirectoryInfo(Path);
    }
}