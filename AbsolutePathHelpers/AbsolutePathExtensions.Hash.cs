using System.Security.Cryptography;

namespace AbsolutePathHelpers;

public static partial class AbsolutePathExtensions
{
    /// <summary>
    /// Computes a cryptographic hash of the specified file using the provided hash algorithm.
    /// </summary>
    /// <param name="absolutePath">The absolute path to the file to be hashed.</param>
    /// <param name="hashAlgorithm">The cryptographic hash algorithm to use for computation.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a lowercase hexadecimal string.</returns>
    /// <remarks>
    /// This method opens the file with read sharing enabled, allowing the file to be read by other processes during hashing.
    /// </remarks>
    public static async Task<string> GetHash(this AbsolutePath absolutePath, HashAlgorithm hashAlgorithm, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(absolutePath.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        byte[] hashBytes = await hashAlgorithm.ComputeHashAsync(stream, cancellationToken);
#if NET9_0_OR_GREATER
        return Convert.ToHexStringLower(hashBytes);
#else
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
#endif
    }

    /// <summary>
    /// Computes the MD5 hash of the specified file.
    /// </summary>
    /// <param name="absolutePath">The absolute path to the file to be hashed.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the MD5 hash as a lowercase hexadecimal string (32 characters).</returns>
    /// <remarks>
    /// MD5 is considered cryptographically broken and should not be used for security purposes.
    /// It is still useful for file integrity checking where collision resistance is not required.
    /// </remarks>
    public static Task<string> GetHashMD5(this AbsolutePath absolutePath, CancellationToken cancellationToken = default)
    {
        return GetHash(absolutePath, MD5.Create(), cancellationToken);
    }

    /// <summary>
    /// Computes the SHA1 hash of the specified file.
    /// </summary>
    /// <param name="absolutePath">The absolute path to the file to be hashed.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the SHA1 hash as a lowercase hexadecimal string (40 characters).</returns>
    /// <remarks>
    /// SHA1 is no longer considered secure for cryptographic purposes where collision resistance is important.
    /// It can still be used for file integrity verification in non-security-critical applications.
    /// </remarks>
    public static Task<string> GetHashSHA1(this AbsolutePath absolutePath, CancellationToken cancellationToken = default)
    {
        return GetHash(absolutePath, SHA1.Create(), cancellationToken);
    }

    /// <summary>
    /// Computes the SHA256 hash of the specified file.
    /// </summary>
    /// <param name="absolutePath">The absolute path to the file to be hashed.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the SHA256 hash as a lowercase hexadecimal string (64 characters).</returns>
    /// <remarks>
    /// SHA256 is a widely used cryptographic hash function that provides strong collision resistance,
    /// making it suitable for security-critical applications and file integrity verification.
    /// </remarks>
    public static Task<string> GetHashSHA256(this AbsolutePath absolutePath, CancellationToken cancellationToken = default)
    {
        return GetHash(absolutePath, SHA256.Create(), cancellationToken);
    }

    /// <summary>
    /// Computes the SHA512 hash of the specified file.
    /// </summary>
    /// <param name="absolutePath">The absolute path to the file to be hashed.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the SHA512 hash as a lowercase hexadecimal string (128 characters).</returns>
    /// <remarks>
    /// SHA512 provides a high level of security with its 512-bit hash size.
    /// It is suitable for applications requiring the strongest security and collision resistance,
    /// though it may be slower than SHA256 for large files.
    /// </remarks>
    public static Task<string> GetHashSHA512(this AbsolutePath absolutePath, CancellationToken cancellationToken = default)
    {
        return GetHash(absolutePath, SHA512.Create(), cancellationToken);
    }
}