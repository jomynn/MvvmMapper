using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace MvvmMapper.Core.Parsing;

/// <summary>Parses a XAML file and extracts DataContext declarations, command bindings, data templates, and child control references.</summary>
public sealed class XamlParser
{
    private static readonly string s_wpfPresentationNs = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly string s_wpfXamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly string s_wpfCompatNs = "http://schemas.openxmlformats.org/markup-compatibility/2006";

    private readonly IFileSystem _fs;
    private readonly ILogger<XamlParser> _logger;

    public XamlParser(IFileSystem fs, ILogger<XamlParser> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    /// <summary>Tries to parse the given XAML file path. Returns null on error.</summary>
    public XamlDocument? TryParse(string filePath)
    {
        try
        {
            var content = _fs.ReadAllText(filePath);
            return Parse(filePath, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse XAML file: {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>Parses XAML content string directly (useful for testing without file I/O).</summary>
    public XamlDocument Parse(string filePath, string xamlContent)
    {
        var doc = XDocument.Parse(xamlContent, LoadOptions.SetLineInfo);
        var root = doc.Root!;

        // Build two maps from the xmlns declarations on the root element:
        //   xmlnsMap:      prefix → CLR namespace (e.g. "vm" → "MyApp.ViewModels")
        //   uriToClrNs:   full xmlns URI value → CLR namespace
        //                 (XLinq uses the raw xmlns value as the XNamespace URI)
        BuildXmlnsMaps(root,
            out var xmlnsMap,
            out var uriToClrNs);

        var designTimePrefixes = GetDesignTimePrefixes(root);

        var xClass = root.Attribute(XNamespace.Get(s_wpfXamlNs) + "Class")?.Value;
        var rootElementType = root.Name.LocalName;

        var dataContexts = new List<XamlDataContextInfo>();
        var commandBindings = new List<XamlCommandBinding>();
        var dataTemplates = new List<XamlDataTemplateInfo>();
        var childControlTypeNames = new List<string>();

        ProcessElement(
            root,
            xmlnsMap,
            uriToClrNs,
            designTimePrefixes,
            dataContexts,
            commandBindings,
            dataTemplates,
            childControlTypeNames,
            isRoot: true);

        return new XamlDocument(
            filePath,
            xClass,
            rootElementType,
            xmlnsMap,
            dataContexts,
            commandBindings,
            dataTemplates,
            childControlTypeNames);
    }

    // -------------------------------------------------------------------------
    // Build xmlns maps
    // -------------------------------------------------------------------------

    private static void BuildXmlnsMaps(
        XElement root,
        out IReadOnlyDictionary<string, string> xmlnsMap,
        out Dictionary<string, string> uriToClrNs)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var uri2clr = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var attr in root.Attributes())
        {
            if (attr.Name.Namespace != XNamespace.Xmlns) continue;

            var prefix = attr.Name.LocalName;
            var uriValue = attr.Value; // e.g. "clr-namespace:MyApp.ViewModels;assembly=MyApp"

            if (!uriValue.StartsWith("clr-namespace:", StringComparison.Ordinal)) continue;

            // Extract the CLR namespace (strip assembly qualifier)
            var ns = uriValue["clr-namespace:".Length..];
            var semicolonIdx = ns.IndexOf(';');
            if (semicolonIdx >= 0) ns = ns[..semicolonIdx];

            map[prefix] = ns;
            // XLinq exposes the raw attribute value as the element's NamespaceName
            uri2clr[uriValue] = ns;
        }

        xmlnsMap = map;
        uriToClrNs = uri2clr;
    }

    // -------------------------------------------------------------------------
    // Design-time prefix detection
    // -------------------------------------------------------------------------

    private static HashSet<string> GetDesignTimePrefixes(XElement root)
    {
        var set = new HashSet<string>(StringComparer.Ordinal) { "d" };

        foreach (var attr in root.Attributes())
        {
            if (attr.Name.Namespace != XNamespace.Xmlns) continue;
            var v = attr.Value;
            if (v.Contains("blend/2008", StringComparison.OrdinalIgnoreCase) ||
                v.Contains("expression/blend", StringComparison.OrdinalIgnoreCase) ||
                v.Contains("designtime", StringComparison.OrdinalIgnoreCase))
            {
                set.Add(attr.Name.LocalName);
            }
        }

        return set;
    }

    // -------------------------------------------------------------------------
    // Element traversal
    // -------------------------------------------------------------------------

    private void ProcessElement(
        XElement element,
        IReadOnlyDictionary<string, string> xmlnsMap,
        Dictionary<string, string> uriToClrNs,
        HashSet<string> designTimePrefixes,
        List<XamlDataContextInfo> dataContexts,
        List<XamlCommandBinding> commandBindings,
        List<XamlDataTemplateInfo> dataTemplates,
        List<string> childControlTypeNames,
        bool isRoot = false)
    {
        var localName = element.Name.LocalName;
        var elementNsUri = element.Name.NamespaceName;

        // ── Explicit DataContext child element: <Foo.DataContext><vm:Bar /></Foo.DataContext>
        if (localName.EndsWith(".DataContext", StringComparison.Ordinal))
        {
            var child = element.Elements().FirstOrDefault();
            if (child != null)
            {
                var childNsUri = child.Name.NamespaceName;
                var clrNs = uriToClrNs.TryGetValue(childNsUri, out var ns) ? ns : null;
                dataContexts.Add(new XamlDataContextInfo(
                    child.Name.LocalName,
                    clrNs,
                    DataContextKind.ExplicitElement,
                    GetLineNumber(child)));
            }
            // Don't recurse into DataContext property elements — their children are the VM instance
            return;
        }

        // ── DataContext attribute binding
        var dcAttr = element.Attribute("DataContext");
        if (dcAttr != null)
        {
            var val = dcAttr.Value;
            if (val.StartsWith("{Binding", StringComparison.Ordinal))
            {
                var info = ParseDataContextBinding(val, GetLineNumber(element));
                if (info != null) dataContexts.Add(info);
            }
        }

        // ── Command="{Binding ...}" on any element
        var cmdAttr = element.Attribute("Command");
        if (cmdAttr?.Value.StartsWith("{Binding", StringComparison.Ordinal) == true)
        {
            var name = ExtractBindingPath(cmdAttr.Value);
            if (!string.IsNullOrEmpty(name))
                commandBindings.Add(new XamlCommandBinding(name, localName, GetLineNumber(element)));
        }

        // ── DataTemplate with DataType="{x:Type ...}"
        if (localName == "DataTemplate")
        {
            var dataTypeAttr = element.Attribute("DataType");
            if (dataTypeAttr != null)
            {
                var dtInfo = ParseDataTemplate(element, dataTypeAttr.Value, xmlnsMap, uriToClrNs);
                if (dtInfo != null) dataTemplates.Add(dtInfo);
            }
        }

        // ── Child custom control detection
        // Skip the root element, WPF built-in namespaces, and property-element syntax (Foo.Bar)
        if (!isRoot &&
            !localName.Contains('.') &&
            !string.IsNullOrEmpty(elementNsUri) &&
            elementNsUri != s_wpfPresentationNs &&
            elementNsUri != s_wpfXamlNs &&
            elementNsUri != s_wpfCompatNs &&
            uriToClrNs.ContainsKey(elementNsUri))
        {
            if (!childControlTypeNames.Contains(localName))
                childControlTypeNames.Add(localName);
        }

        // ── Recurse into children
        foreach (var child in element.Elements())
        {
            ProcessElement(
                child,
                xmlnsMap,
                uriToClrNs,
                designTimePrefixes,
                dataContexts,
                commandBindings,
                dataTemplates,
                childControlTypeNames,
                isRoot: false);
        }
    }

    // -------------------------------------------------------------------------
    // DataContext binding parsing
    // -------------------------------------------------------------------------

    private static XamlDataContextInfo? ParseDataContextBinding(string bindingExpr, int lineNumber)
    {
        if (bindingExpr.Contains("StaticResource", StringComparison.Ordinal))
        {
            // {Binding LoginVM, Source={StaticResource Locator}}
            var path = ExtractBindingPath(bindingExpr);
            return new XamlDataContextInfo(
                path ?? string.Empty,
                null,
                DataContextKind.LocatorBinding,
                lineNumber);
        }

        var bindPath = ExtractBindingPath(bindingExpr);
        if (string.IsNullOrEmpty(bindPath)) return null;

        return new XamlDataContextInfo(bindPath, null, DataContextKind.BindingPath, lineNumber);
    }

    private static string? ExtractBindingPath(string bindingExpr)
    {
        // Strip surrounding braces: "{Binding Foo, ...}" → "Binding Foo, ..."
        var inner = bindingExpr.Trim();
        if (inner.StartsWith('{')) inner = inner[1..];
        if (inner.EndsWith('}')) inner = inner[..^1];
        inner = inner.Trim();

        // Strip "Binding" keyword
        if (!inner.StartsWith("Binding", StringComparison.Ordinal)) return null;
        inner = inner["Binding".Length..].TrimStart(',').Trim();

        if (string.IsNullOrEmpty(inner)) return null;

        // Handle explicit "Path=Foo"
        if (inner.StartsWith("Path=", StringComparison.OrdinalIgnoreCase))
            inner = inner["Path=".Length..].Trim();

        // Take up to the first comma, space, or closing brace — that's the path token
        var end = inner.IndexOfAny([',', ' ', '}']);
        return end >= 0 ? inner[..end].Trim() : inner.Trim();
    }

    // -------------------------------------------------------------------------
    // DataTemplate parsing
    // -------------------------------------------------------------------------

    private static XamlDataTemplateInfo? ParseDataTemplate(
        XElement element,
        string dataTypeValue,
        IReadOnlyDictionary<string, string> xmlnsMap,
        Dictionary<string, string> uriToClrNs)
    {
        // DataType="{x:Type vm:FooViewModel}"
        if (!dataTypeValue.Contains("x:Type", StringComparison.Ordinal)) return null;

        var inner = dataTypeValue.Trim();
        if (inner.StartsWith('{')) inner = inner[1..];
        if (inner.EndsWith('}')) inner = inner[..^1];
        inner = inner.Trim();

        // Strip "x:Type"
        var spaceIdx = inner.IndexOf(' ');
        if (spaceIdx < 0) return null;
        var typeRef = inner[(spaceIdx + 1)..].Trim(); // "vm:FooViewModel"

        var (vmPrefix, vmTypeName) = SplitPrefixedName(typeRef);
        var vmNs = vmPrefix != null && xmlnsMap.TryGetValue(vmPrefix, out var vns) ? vns : null;

        // Find the first child element that belongs to a CLR namespace (the view)
        var childView = element.Elements().FirstOrDefault(e =>
            !string.IsNullOrEmpty(e.Name.NamespaceName) &&
            uriToClrNs.ContainsKey(e.Name.NamespaceName));

        string viewTypeName = childView?.Name.LocalName ?? string.Empty;
        string? viewNs = null;
        if (childView != null &&
            uriToClrNs.TryGetValue(childView.Name.NamespaceName, out var vViewNs))
        {
            viewNs = vViewNs;
        }

        return new XamlDataTemplateInfo(viewTypeName, viewNs, vmTypeName, vmNs, GetLineNumber(element));
    }

    // -------------------------------------------------------------------------
    // Utilities
    // -------------------------------------------------------------------------

    private static (string? prefix, string localName) SplitPrefixedName(string name)
    {
        var idx = name.IndexOf(':');
        return idx >= 0 ? (name[..idx], name[(idx + 1)..]) : (null, name);
    }

    private static int GetLineNumber(XObject node) =>
        node is IXmlLineInfo li && li.HasLineInfo() ? li.LineNumber : 0;
}
