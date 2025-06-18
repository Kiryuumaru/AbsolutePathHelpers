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
    /// Asynchronously writes text content to a file at the specified path, creating the file if it doesn't exist.
    /// </summary>
    /// <param name="absolutePath">The absolute path to the file to write to.</param>
    /// <param name="content">The text content to write to the file.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    /// <exception cref="IOException">The file is being used by another process, or an I/O error occurred.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller doesn't have the required permission.</exception>
    /// <remarks>
    /// If the target file already exists, it will be overwritten. The parent directory will be created 
    /// automatically if it doesn't exist.
    /// </remarks>
    public static async Task WriteAllText(this AbsolutePath absolutePath, string content, CancellationToken cancellationToken = default)
    {
        absolutePath.Parent?.CreateDirectory();
        await File.WriteAllTextAsync(absolutePath.Path, content, cancellationToken);
    }

    /// <summary>
    /// Creates a directory at the specified path if it doesn't already exist.
    /// </summary>
    /// <param name="absolutePath">The absolute path where the directory should be created.</param>
    /// <exception cref="IOException">The directory cannot be created.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller doesn't have the required permission.</exception>
    /// <remarks>
    /// This method does nothing if the directory already exists. All parent directories will also
    /// be created if they don't exist.
    /// </remarks>
    public static void CreateDirectory(this AbsolutePath absolutePath)
    {
        if (!absolutePath.DirectoryExists())
        {
            Directory.CreateDirectory(absolutePath.Path);
        }
    }

    /// <summary>
    /// Creates a directory at the specified path, or empties it if it already exists.
    /// </summary>
    /// <param name="absolutePath">The absolute path where the directory should be created or cleaned.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="IOException">The directory cannot be created or cleaned.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller doesn't have the required permission.</exception>
    /// <remarks>
    /// If the directory already exists, all its contents (files, subdirectories, and symbolic links) will be deleted
    /// before returning. This ensures you have an empty directory to work with.
    /// </remarks>
    public static async Task CreateOrCleanDirectory(this AbsolutePath absolutePath, CancellationToken cancellationToken = default)
    {
        await Delete(absolutePath, cancellationToken);
        CreateDirectory(absolutePath);
    }

    /// <summary>
    /// Creates an empty file or updates the timestamp of an existing file (similar to the Unix "touch" command).
    /// </summary>
    /// <param name="absolutePath">The absolute path to the file to create or update.</param>
    /// <param name="time">The timestamp to set on the file. If null, the current system time is used.</param>
    /// <param name="createDirectories">If true, ensures parent directories exist; otherwise, may fail if they don't.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="IOException">An I/O error occurred while creating or updating the file.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller doesn't have the required permission.</exception>
    /// <remarks>
    /// If the file doesn't exist, an empty file will be created. If the file already exists, only its
    /// last write time will be updated, preserving the file's content.
    /// </remarks>
    public static async Task TouchFile(this AbsolutePath absolutePath, DateTime? time = null, bool createDirectories = true, CancellationToken cancellationToken = default)
    {
        if (createDirectories)
        {
            absolutePath.Parent?.CreateDirectory();
        }

        if (!File.Exists(absolutePath.Path))
        {
            await File.WriteAllBytesAsync(absolutePath.Path, [], cancellationToken);
        }

        File.SetLastWriteTime(absolutePath.Path, time ?? DateTime.Now);
    }

    /// <summary>
    /// Recursively copies a file or directory (including all contents) to a target location.
    /// </summary>
    /// <param name="path">The source path (file or directory) to copy from.</param>
    /// <param name="targetPath">The target path to copy to.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation. The result is true if the copy was successful, false if the source doesn't exist.</returns>
    /// <exception cref="IOException">An I/O error occurred during the copy operation.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller doesn't have the required permission.</exception>
    /// <remarks>
    /// When copying a file, the parent directory of the target will be created if it doesn't exist.
    /// When copying a directory, all subdirectories, files, and symbolic links will be copied.
    /// If the target already exists, it will be overwritten.
    /// 
    /// Symbolic links will be recreated as symbolic links at the target location, preserving their targets.
    /// If a symbolic link points to a location within the source directory, its target will be adjusted
    /// to point to the corresponding location in the target directory.
    /// </remarks>
    public static async Task<bool> CopyTo(this AbsolutePath path, AbsolutePath targetPath, CancellationToken cancellationToken = default)
    {
        if (path.FileExists())
        {
            targetPath.Parent?.CreateDirectory();

            File.Copy(path.ToString(), targetPath.ToString(), true);

            return true;
        }
        else if (path.DirectoryExists())
        {
            var fileMap = GetFileMap(path);

            Directory.CreateDirectory(targetPath);
            foreach (var folder in fileMap.Folders)
            {
                AbsolutePath target = folder.ToString().Replace(path, targetPath);
                Directory.CreateDirectory(target);
            }
            foreach (var file in fileMap.Files)
            {
                AbsolutePath target = file.ToString().Replace(path, targetPath);
                target.Parent?.CreateDirectory();
                File.Copy(file, target, true);
            }
            foreach (var (Link, Target) in fileMap.SymbolicLinks)
            {
                AbsolutePath newLink = Link.ToString().Replace(path, targetPath);
                string newTarget;
                if (path.IsParentOf(Target))
                {
                    newTarget = Target.ToString().Replace(path, targetPath);
                }
                else
                {
                    newTarget = Target;
                }

                await newLink.Delete(cancellationToken);
                newLink.Parent?.CreateDirectory();

                if (Target.DirectoryExists() || Link.DirectoryExists())
                {
                    Directory.CreateSymbolicLink(newLink, newTarget);
                }
                else
                {
                    File.CreateSymbolicLink(newLink, newTarget);
                }
            }

            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Recursively moves a file or directory (including all contents) to a target location.
    /// </summary>
    /// <param name="path">The source path (file or directory) to move from.</param>
    /// <param name="targetPath">The target path to move to.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation. The result is true if the move was successful, false if the source doesn't exist.</returns>
    /// <exception cref="IOException">An I/O error occurred during the move operation.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller doesn't have the required permission.</exception>
    /// <remarks>
    /// When moving a file, the parent directory of the target will be created if it doesn't exist.
    /// When moving a directory, all subdirectories, files, and symbolic links will be moved.
    /// If the target already exists, it will be overwritten.
    /// 
    /// This operation first copies everything to the target location and then deletes the source.
    /// Symbolic links will be recreated as symbolic links at the target location, preserving their targets.
    /// If a symbolic link points to a location within the source directory, its target will be adjusted
    /// to point to the corresponding location in the target directory.
    /// </remarks>
    public static async Task<bool> MoveTo(this AbsolutePath path, AbsolutePath targetPath, CancellationToken cancellationToken = default)
    {
        if (path.FileExists())
        {
            targetPath.Parent?.CreateDirectory();
            File.Move(path.ToString(), targetPath.ToString(), true);

            return true;
        }
        else if (path.DirectoryExists())
        {
            var fileMap = GetFileMap(path);

            Directory.CreateDirectory(targetPath);
            foreach (var folder in fileMap.Folders)
            {
                AbsolutePath target = folder.ToString().Replace(path, targetPath);
                Directory.CreateDirectory(target);
            }
            foreach (var file in fileMap.Files)
            {
                AbsolutePath target = file.ToString().Replace(path, targetPath);
                target.Parent?.CreateDirectory();
                File.Move(file, target, true);
            }
            foreach (var (Link, Target) in fileMap.SymbolicLinks)
            {
                AbsolutePath newLink = Link.ToString().Replace(path, targetPath);
                string newTarget;
                if (path.IsParentOf(Target))
                {
                    newTarget = Target.ToString().Replace(path, targetPath);
                }
                else
                {
                    newTarget = Target;
                }

                await newLink.Delete(cancellationToken);
                newLink.Parent?.CreateDirectory();

                if (Target.DirectoryExists() || Link.DirectoryExists())
                {
                    Directory.CreateSymbolicLink(newLink, newTarget);
                }
                else
                {
                    File.CreateSymbolicLink(newLink, newTarget);
                }
            }

            await path.Delete(cancellationToken);

            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Recursively deletes a file or directory (including all contents).
    /// </summary>
    /// <param name="path">The path (file or directory) to delete.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation. The result is true if the deletion was successful, false if the path doesn't exist.</returns>
    /// <exception cref="IOException">An I/O error occurred during the deletion, such as the file being locked.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller doesn't have the required permission.</exception>
    /// <remarks>
    /// When deleting a directory, all contained files, subdirectories, and symbolic links will be deleted.
    /// This method is safe to call on paths that don't exist - it will simply return false.
    /// 
    /// Note that this operation runs on a background thread via Task.Run to avoid blocking the calling thread
    /// during potentially long-running deletion operations.
    /// </remarks>
    public static Task<bool> Delete(this AbsolutePath path, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (path.FileExists())
            {
                File.Delete(path);

                return true;
            }
            else if (path.DirectoryExists())
            {
                var fileMap = GetFileMap(path);

                foreach (var (Link, _) in fileMap.SymbolicLinks)
                {
                    if (Link.DirectoryExists())
                    {
                        Directory.Delete(Link);
                    }
                    else
                    {
                        File.Delete(Link);
                    }
                }

                Directory.Delete(path, true);

                return true;
            }
            else
            {
                return false;
            }
        }, cancellationToken);
    }
}