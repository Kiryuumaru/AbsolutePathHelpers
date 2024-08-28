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
    /// <exception cref="ArgumentException">Thrown when the path is null, empty, or not an absolute path.</exception>
    public static AbsolutePath Create(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty", nameof(path));
        }

        if (!System.IO.Path.IsPathRooted(path))
        {
            throw new ArgumentException("Path must be an absolute path", nameof(path));
        }

        return new AbsolutePath(path);
    }

    /// <summary>
    /// Gets or sets the absolute path.
    /// </summary>
    public string Path { get; } = path;

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
    /// Combines the current <see cref="AbsolutePath"/> with a relative path.
    /// </summary>
    /// <param name="b">The base <see cref="AbsolutePath"/>.</param>
    /// <param name="c">The relative path to combine with.</param>
    /// <returns>A new <see cref="AbsolutePath"/> representing the combined path.</returns>
    public static AbsolutePath operator /(AbsolutePath b, string c)
    {
        return new AbsolutePath(System.IO.Path.Combine(b.Path, c));
    }

    /// <summary>
    /// Implicitly converts a string to an <see cref="AbsolutePath"/>.
    /// </summary>
    /// <param name="path">The string path to convert.</param>
    public static implicit operator AbsolutePath(string path) => new(path);

    /// <summary>
    /// Implicitly converts an <see cref="AbsolutePath"/> to a string.
    /// </summary>
    /// <param name="path">The <see cref="AbsolutePath"/> to convert.</param>
    public static implicit operator string(AbsolutePath path) => path.Path;

    /// <summary>
    /// Implicitly converts an <see cref="AbsolutePath"/> to a <see cref="FileInfo"/>.
    /// </summary>
    /// <param name="path">The <see cref="AbsolutePath"/> to convert.</param>
    public static implicit operator FileInfo?(AbsolutePath path) => path.ToFileInfo();

    /// <summary>
    /// Implicitly converts an <see cref="AbsolutePath"/> to a <see cref="DirectoryInfo"/>.
    /// </summary>
    /// <param name="path">The <see cref="AbsolutePath"/> to convert.</param>
    public static implicit operator DirectoryInfo?(AbsolutePath path) => path.ToDirectoryInfo();

    /// <summary>
    /// Determines whether two <see cref="AbsolutePath"/> instances are equal.
    /// </summary>
    /// <param name="left">The left <see cref="AbsolutePath"/> to compare.</param>
    /// <param name="right">The right <see cref="AbsolutePath"/> to compare.</param>
    /// <returns><c>true</c> if the specified <see cref="AbsolutePath"/> instances are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(AbsolutePath? left, AbsolutePath? right)
    {
        return EqualityComparer<AbsolutePath>.Default.Equals(left, right);
    }

    /// <summary>
    /// Determines whether two <see cref="AbsolutePath"/> instances are not equal.
    /// </summary>
    /// <param name="left">The left <see cref="AbsolutePath"/> to compare.</param>
    /// <param name="right">The right <see cref="AbsolutePath"/> to compare.</param>
    /// <returns><c>true</c> if the specified <see cref="AbsolutePath"/> instances are not equal; otherwise, <c>false</c>.</returns>
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
}
