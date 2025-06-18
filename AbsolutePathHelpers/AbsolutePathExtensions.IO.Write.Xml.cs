using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

#if NETSTANDARD
#elif NET5_0_OR_GREATER
using static AbsolutePathHelpers.Common.Internals.Message;
#endif

namespace AbsolutePathHelpers;

public static partial class AbsolutePathExtensions
{
    /// <summary>
    /// Asynchronously serializes an object to XML and writes it to a file at the specified path.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize to XML.</typeparam>
    /// <param name="absolutePath">The absolute path to the file where the XML will be written.</param>
    /// <param name="obj">The object to serialize to XML.</param>
    /// <param name="xmlWriterSettings">Optional settings to control the behavior of the XML writer.</param>
    /// <param name="namespaces">Optional XML namespace declarations to include in the root element.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    /// <exception cref="IOException">An I/O error occurred while writing the file.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller doesn't have the required permission.</exception>
    /// <exception cref="InvalidOperationException">An error occurred during XML serialization.</exception>
    /// <remarks>
    /// This method creates the parent directory if it doesn't exist. If the file already exists,
    /// it will be overwritten. The file is created with <see cref="FileMode.Create"/> mode.
    /// 
    /// The XML serializer uses the default settings unless custom configuration is provided 
    /// through the <paramref name="xmlWriterSettings"/> parameter.
    /// 
    /// This method requires dynamic code generation capabilities which may be restricted in some environments.
    /// </remarks>
#if NET7_0_OR_GREATER
    [RequiresDynamicCode(RequiresDynamicCode)]
#endif
    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    public static async Task WriteXml<T>(this AbsolutePath absolutePath, T obj, XmlWriterSettings? xmlWriterSettings = null, XmlSerializerNamespaces? namespaces = null, CancellationToken cancellationToken = default)
    {
        absolutePath.Parent?.CreateDirectory();
        await using var fileStream = new FileStream(absolutePath.Path, FileMode.Create, FileAccess.Write);
        
        // Create settings if null and enable async
        var settings = xmlWriterSettings ?? new XmlWriterSettings();
        settings.Async = true;
        
        await using var xmlWriter = XmlWriter.Create(fileStream, settings);
        var serializer = new XmlSerializer(typeof(T));
        
        // Check for cancellation before potentially long operation
        cancellationToken.ThrowIfCancellationRequested();
        
        if (namespaces != null)
        {
            serializer.Serialize(xmlWriter, obj, namespaces);
        }
        else
        {
            serializer.Serialize(xmlWriter, obj);
        }
        
        await xmlWriter.FlushAsync();
    }

    /// <summary>
    /// Asynchronously writes an <see cref="XmlDocument"/> to a file at the specified path.
    /// </summary>
    /// <param name="absolutePath">The absolute path to the file where the XML will be written.</param>
    /// <param name="xmlDocument">The <see cref="XmlDocument"/> to write to the file.</param>
    /// <param name="xmlWriterSettings">Optional settings to control the behavior of the XML writer.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    /// <exception cref="IOException">An I/O error occurred while writing the file.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller doesn't have the required permission.</exception>
    /// <exception cref="ArgumentNullException">The <paramref name="xmlDocument"/> is null.</exception>
    /// <exception cref="XmlException">An error occurred during XML writing.</exception>
    /// <remarks>
    /// This method creates the parent directory if it doesn't exist. If the file already exists,
    /// it will be overwritten. The file is created with <see cref="FileMode.Create"/> mode.
    /// 
    /// <see cref="XmlDocument"/> is part of the standard .NET XML DOM API. Use this method when working
    /// with XML data that has been manipulated using the traditional DOM API.
    /// 
    /// The XML writer settings can be used to control formatting, indentation, encoding, and other
    /// aspects of the XML output.
    /// </remarks>
    public static async Task WriteXmlDocument(this AbsolutePath absolutePath, XmlDocument xmlDocument, XmlWriterSettings? xmlWriterSettings = null, CancellationToken cancellationToken = default)
    {
        absolutePath.Parent?.CreateDirectory();
        await using var fileStream = new FileStream(absolutePath.Path, FileMode.Create, FileAccess.Write);
        
        // Create settings if null and enable async
        var settings = xmlWriterSettings ?? new XmlWriterSettings();
        settings.Async = true;
        
        await using var xmlWriter = XmlWriter.Create(fileStream, settings);
        
        // XmlDocument.Save is not async, so we need to wrap it in a Task.Run
        await Task.Run(() => xmlDocument.Save(xmlWriter), cancellationToken);
        
        await xmlWriter.FlushAsync();
    }

    /// <summary>
    /// Asynchronously writes an <see cref="XDocument"/> to a file at the specified path.
    /// </summary>
    /// <param name="absolutePath">The absolute path to the file where the XML will be written.</param>
    /// <param name="xDocument">The <see cref="XDocument"/> to write to the file.</param>
    /// <param name="options">Options for saving the XML document.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    /// <exception cref="IOException">An I/O error occurred while writing the file.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller doesn't have the required permission.</exception>
    /// <exception cref="ArgumentNullException">The <paramref name="xDocument"/> is null.</exception>
    /// <exception cref="XmlException">An error occurred during XML writing.</exception>
    /// <remarks>
    /// This method creates the parent directory if it doesn't exist. If the file already exists,
    /// it will be overwritten. The file is created with <see cref="FileMode.Create"/> mode.
    /// 
    /// <see cref="XDocument"/> is part of LINQ to XML and provides a lightweight, LINQ-friendly API
    /// for working with XML data. Use this method when working with XML data that has been 
    /// created or manipulated using LINQ to XML.
    /// 
    /// The save options can be used to control aspects of the XML output such as whether to:
    /// <list type="bullet">
    ///   <item><description>Disable formatting (SaveOptions.DisableFormatting)</description></item>
    ///   <item><description>Omit the XML declaration (SaveOptions.OmitDuplicateNamespaces)</description></item>
    ///   <item><description>Omit duplicate namespace declarations (SaveOptions.OmitDuplicateNamespaces)</description></item>
    /// </list>
    /// </remarks>
    public static async Task WriteXDocument(this AbsolutePath absolutePath, XDocument xDocument, SaveOptions options = SaveOptions.None, CancellationToken cancellationToken = default)
    {
        absolutePath.Parent?.CreateDirectory();
        await using var fileStream = new FileStream(absolutePath.Path, FileMode.Create, FileAccess.Write);
        
#if NET8_0_OR_GREATER
        // Use the built-in async method with cancellation in .NET 8+
        await xDocument.SaveAsync(fileStream, options, cancellationToken);
#else
        // For older versions, need to wrap with Task.Run
        await Task.Run(() => xDocument.Save(fileStream, options), cancellationToken);
#endif
    }

    /// <summary>
    /// Asynchronously writes an <see cref="XElement"/> to a file at the specified path.
    /// </summary>
    /// <param name="absolutePath">The absolute path to the file where the XML will be written.</param>
    /// <param name="xElement">The <see cref="XElement"/> to write to the file.</param>
    /// <param name="options">Options for saving the XML element.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    /// <exception cref="IOException">An I/O error occurred while writing the file.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller doesn't have the required permission.</exception>
    /// <exception cref="ArgumentNullException">The <paramref name="xElement"/> is null.</exception>
    /// <exception cref="XmlException">An error occurred during XML writing.</exception>
    /// <remarks>
    /// This method creates the parent directory if it doesn't exist. If the file already exists,
    /// it will be overwritten. The file is created with <see cref="FileMode.Create"/> mode.
    /// 
    /// <see cref="XElement"/> is part of LINQ to XML and represents a single XML element with
    /// its attributes and content. Use this method when you want to save a specific XML element
    /// (and its descendants) as the root of an XML document.
    /// 
    /// The save options can be used to control aspects of the XML output such as whether to:
    /// <list type="bullet">
    ///   <item><description>Disable formatting (SaveOptions.DisableFormatting)</description></item>
    ///   <item><description>Omit duplicate namespace declarations (SaveOptions.OmitDuplicateNamespaces)</description></item>
    /// </list>
    /// </remarks>
    public static async Task WriteXElement(this AbsolutePath absolutePath, XElement xElement, SaveOptions options = SaveOptions.None, CancellationToken cancellationToken = default)
    {
        absolutePath.Parent?.CreateDirectory();
        await using var fileStream = new FileStream(absolutePath.Path, FileMode.Create, FileAccess.Write);
        
#if NET8_0_OR_GREATER
        // Use the built-in async method with cancellation in .NET 8+
        await xElement.SaveAsync(fileStream, options, cancellationToken);
#else
        // For older versions, need to wrap with Task.Run
        await Task.Run(() => xElement.Save(fileStream, options), cancellationToken);
#endif
    }
}