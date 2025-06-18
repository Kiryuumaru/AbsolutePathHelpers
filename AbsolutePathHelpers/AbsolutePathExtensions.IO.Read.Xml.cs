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
    /// Asynchronously reads and deserializes an XML file into an object of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the XML content into.</typeparam>
    /// <param name="absolutePath">The absolute path to the XML file to read.</param>
    /// <param name="xmlReaderSettings">Optional settings to control the behavior of the XML reader.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the deserialized object.</returns>
    /// <exception cref="FileNotFoundException">The file specified by <paramref name="absolutePath"/> was not found.</exception>
    /// <exception cref="InvalidOperationException">The XML content is not compatible with type <typeparamref name="T"/>.</exception>
    /// <exception cref="XmlException">The XML is invalid or malformed.</exception>
    /// <remarks>
    /// This method uses XML serialization to convert the file content into an object of type <typeparamref name="T"/>.
    /// The type <typeparamref name="T"/> must be serializable by the <see cref="XmlSerializer"/>.
    /// 
    /// This method requires dynamic code generation capabilities which may be restricted in some environments.
    /// </remarks>
#if NET7_0_OR_GREATER
    [RequiresDynamicCode(RequiresDynamicCode)]
#endif
    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    public static async Task<T?> ReadXml<T>(this AbsolutePath absolutePath, XmlReaderSettings? xmlReaderSettings = null, CancellationToken cancellationToken = default)
    {
        await using var fileStream = new FileStream(absolutePath.Path, FileMode.Open, FileAccess.Read);
        
        // Create settings if null and enable async
        var settings = xmlReaderSettings ?? new XmlReaderSettings();
        settings.Async = true;
        
        using var xmlReader = XmlReader.Create(fileStream, settings);
        var serializer = new XmlSerializer(typeof(T));
        
        // Check for cancellation before potentially long operation
        cancellationToken.ThrowIfCancellationRequested();
        
        return (T?)serializer.Deserialize(xmlReader);
    }

    /// <summary>
    /// Asynchronously reads an XML file into an <see cref="XmlDocument"/>.
    /// </summary>
    /// <param name="absolutePath">The absolute path to the XML file to read.</param>
    /// <param name="xmlReaderSettings">Optional settings to control the behavior of the XML reader.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the loaded <see cref="XmlDocument"/>.</returns>
    /// <exception cref="FileNotFoundException">The file specified by <paramref name="absolutePath"/> was not found.</exception>
    /// <exception cref="XmlException">The XML is invalid or malformed.</exception>
    /// <remarks>
    /// This method loads the XML file into an <see cref="XmlDocument"/>, which is part of the standard .NET XML DOM API.
    /// <see cref="XmlDocument"/> provides a comprehensive set of methods for traversing and manipulating XML data.
    /// 
    /// Use this method when you need to work with XML using the traditional DOM API, especially when you need
    /// to perform operations like XPath queries, transformations, or schema validation.
    /// </remarks>
    public static async Task<XmlDocument> ReadXmlDocument(this AbsolutePath absolutePath, XmlReaderSettings? xmlReaderSettings = null, CancellationToken cancellationToken = default)
    {
        await using var fileStream = new FileStream(absolutePath.Path, FileMode.Open, FileAccess.Read);
        
        // Create settings if null and enable async
        var settings = xmlReaderSettings ?? new XmlReaderSettings();
        settings.Async = true;
        
        using var xmlReader = XmlReader.Create(fileStream, settings);
        var xmlDocument = new XmlDocument();
        
        // Create an XmlDocumentWithCancellation implementation
        var loadTask = Task.Run(() => {
            xmlDocument.Load(xmlReader);
            return xmlDocument;
        }, cancellationToken);
        
        return await loadTask;
    }

    /// <summary>
    /// Asynchronously reads an XML file into an <see cref="XDocument"/>.
    /// </summary>
    /// <param name="absolutePath">The absolute path to the XML file to read.</param>
    /// <param name="loadOptions">Options for loading the XML document (e.g., preserving whitespace).</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the loaded <see cref="XDocument"/>.</returns>
    /// <exception cref="FileNotFoundException">The file specified by <paramref name="absolutePath"/> was not found.</exception>
    /// <exception cref="XmlException">The XML is invalid or malformed.</exception>
    /// <remarks>
    /// This method loads the XML file into an <see cref="XDocument"/>, which is part of LINQ to XML.
    /// <see cref="XDocument"/> provides a lightweight, LINQ-friendly API for working with XML data.
    /// 
    /// Use this method when you want to perform LINQ queries against XML data or when you prefer
    /// the more modern and streamlined API provided by LINQ to XML.
    /// </remarks>
    public static async Task<XDocument> ReadXDocument(this AbsolutePath absolutePath, LoadOptions loadOptions = LoadOptions.None, CancellationToken cancellationToken = default)
    {
        await using var fileStream = new FileStream(absolutePath.Path, FileMode.Open, FileAccess.Read);
        
        // Use the built-in async method with cancellation
#if NET8_0_OR_GREATER
        return await XDocument.LoadAsync(fileStream, loadOptions, cancellationToken);
#else
        // For older versions, need to wrap with Task.Run
        var loadTask = Task.Run(() => XDocument.Load(fileStream, loadOptions), cancellationToken);
        return await loadTask;
#endif
    }

    /// <summary>
    /// Asynchronously reads an XML file into an <see cref="XElement"/>.
    /// </summary>
    /// <param name="absolutePath">The absolute path to the XML file to read.</param>
    /// <param name="loadOptions">Options for loading the XML element (e.g., preserving whitespace).</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the loaded root <see cref="XElement"/>.</returns>
    /// <exception cref="FileNotFoundException">The file specified by <paramref name="absolutePath"/> was not found.</exception>
    /// <exception cref="XmlException">The XML is invalid or malformed.</exception>
    /// <remarks>
    /// This method loads the XML file directly into an <see cref="XElement"/>, which represents the root element of the XML document.
    /// <see cref="XElement"/> is part of LINQ to XML and provides a lightweight API for working with XML elements.
    /// 
    /// Use this method when you are only interested in the root element and its descendants, and don't need
    /// document-level information like the XML declaration or document type.
    /// </remarks>
    public static async Task<XElement> ReadXElement(this AbsolutePath absolutePath, LoadOptions loadOptions = LoadOptions.None, CancellationToken cancellationToken = default)
    {
        await using var fileStream = new FileStream(absolutePath.Path, FileMode.Open, FileAccess.Read);
        
        // Use the built-in async method with cancellation
#if NET8_0_OR_GREATER
        return await XElement.LoadAsync(fileStream, loadOptions, cancellationToken);
#else
        // For older versions, need to wrap with Task.Run
        var loadTask = Task.Run(() => XElement.Load(fileStream, loadOptions), cancellationToken);
        return await loadTask;
#endif
    }
}