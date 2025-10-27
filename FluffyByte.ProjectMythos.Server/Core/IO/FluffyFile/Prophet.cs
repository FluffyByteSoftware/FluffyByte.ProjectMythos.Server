using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace FluffyByte.ProjectMythos.Server.Core.IO.FluffyFile;

// The prophet uses reflection to generate HTML documentation for the assembly

/// <summary>
/// Provides functionality to generate HTML documentation for the public types in a .NET assembly.
/// </summary>
/// <remarks>The <see cref="Prophet"/> class processes a specified assembly, filters its public types based on an
/// optional namespace filter,  and generates HTML documentation for those types. The documentation includes details
/// about the types, their members, and their relationships.</remarks>
public class Prophet
{
    private readonly Assembly _assembly;
    private readonly string _outputDir;
    private readonly string _namespaceFilter;
    private readonly Dictionary<string, XElement> _xmlDocs;
    private readonly List<Type> _allTypes;

    /// <summary>
    /// Initializes a new instance of the <see cref="Prophet"/> class, which processes an assembly to generate XML
    /// documentation for its public types.
    /// </summary>
    /// <remarks>This constructor loads the specified assembly and filters its public types based on the
    /// provided namespace filter. It also initializes the XML documentation processing for the assembly.</remarks>
    /// <param name="assemblyPath">The file path to the assembly to be loaded. This must be a valid path to a .NET assembly file.</param>
    /// <param name="outputDir">The directory where the generated XML documentation files will be saved. This must be a valid directory path.</param>
    /// <param name="namespaceFilter">A namespace filter to restrict the types processed to those within the specified namespace or its
    /// sub-namespaces. If <see langword="null"/>, all public types in the assembly will be processed.</param>
    public Prophet(string assemblyPath, string outputDir, string namespaceFilter)
    {
        _assembly = Assembly.LoadFrom(assemblyPath);
        _outputDir = outputDir;
        _namespaceFilter = namespaceFilter;
        _xmlDocs = [];
        LoadXmlDocumentation(assemblyPath);

        _allTypes = [.. _assembly.GetTypes()
            .Where(t => t.IsPublic && (namespaceFilter == null || t.Namespace?.StartsWith(namespaceFilter) == true))
            .OrderBy(t => t.FullName)];
    }

    /// <summary>
    /// Generates documentation files for all types and writes them to the specified output directory.
    /// </summary>
    /// <remarks>This method creates the output directory if it does not already exist, generates an index
    /// page,  and then generates individual documentation pages for each type in the collection.  A summary of the
    /// operation is written to the console upon completion.</remarks>
    public void Generate()
    {
        Directory.CreateDirectory(_outputDir);

        GenerateIndexPage();

        foreach (var type in _allTypes)
        {
            GenerateTypePage(type);
        }

        Console.WriteLine($"Generated documentation for {_allTypes.Count} types in {_outputDir}");
    }

    /// <summary>
    /// Generates an HTML index page for the API documentation.
    /// </summary>
    /// <remarks>This method creates an index page that lists all documented types, grouped by their
    /// namespaces. Each type is displayed with its kind (e.g., class, interface) and a link to its detailed
    /// documentation page. The generated HTML file is saved as "index.html" in the specified output
    /// directory.</remarks>
    private void GenerateIndexPage()
    {
        StringBuilder html = new();
        html.AppendLine("<!DOCTYPE html><html><head>");
        html.AppendLine("<meta charset='utf-8'>");
        html.AppendLine("<title>API Documentation</title>");
        html.AppendLine(GetStyleSheet());
        html.AppendLine("</head><body>");
        html.AppendLine("<h1>API Documentation</h1>");

        var byNamespace = _allTypes.GroupBy(t => t.Namespace ?? "(global)");

        foreach (var ns in byNamespace.OrderBy(g => g.Key))
        {
            html.AppendLine($"<h2>{ns.Key}</h2>");
            html.AppendLine("<ul>");

            foreach (var type in ns.OrderBy(t => t.Name))
            {
                string typeKind = GetTypeKind(type);
                html.AppendLine($"<li><span class='type-kind'>{typeKind}</span> <a href='{GetTypeFileName(type)}'>{type.Name}</a>");

                var summary = GetSummary(type);
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    html.AppendLine($"<div class='summary'>{summary}</div>");
                }
                html.AppendLine("</li>");
            }
            html.AppendLine("</ul>");
        }

        html.AppendLine("</body></html>");
        File.WriteAllText(Path.Combine(_outputDir, "index.html"), html.ToString());
    }

    /// <summary>
    /// Generates an HTML documentation page for the specified type, including its metadata, relationships, and members.
    /// </summary>
    /// <remarks>The generated page includes the type's name, namespace, summary, relationships (such as base
    /// types, implemented interfaces, and derived types), and its members (constructors, properties, and methods). The
    /// output is written to the appropriate directory structure based on the type's namespace.</remarks>
    /// <param name="type">The <see cref="Type"/> for which the documentation page is generated. This must be a valid .NET type.</param>
    private void GenerateTypePage(Type type)
    {
        StringBuilder html = new();
        html.AppendLine("<!DOCTYPE html><html><head>");
        html.AppendLine("<meta charset='utf-8'>");
        html.AppendLine($"<title>{type.Name}</title>");
        html.AppendLine(GetStyleSheet());
        html.AppendLine("</head><body>");

        html.AppendLine($"<div class='header'><a href='{GetRelativeIndexPath(type)}'>← Back to Index</a></div>");
        html.AppendLine($"<h1>{GetTypeKind(type)} {type.Name}</h1>");
        html.AppendLine($"<div class='namespace'>Namespace: {type.Namespace ?? "(global)"}</div>");

        var summary = GetSummary(type);

        if (!string.IsNullOrWhiteSpace(summary))
        {
            html.AppendLine($"<div class='type-summary'>{summary}</div>");
        }

        html.AppendLine("<h2>Relationships</h2>");
        html.AppendLine("<div class='relationships'>");

        if (type.BaseType != null && type.BaseType != typeof(object))
        {
            html.AppendLine($"<div><strong>Inherits from:</strong> {FormatTypeLink(type.BaseType)}</div>");
        }

        var interfaces = type.GetInterfaces();
        if (interfaces.Length > 0)
        {
            html.AppendLine("<div><strong>Implements:</strong> ");
            html.AppendLine(string.Join(", ", interfaces.Select(FormatTypeLink)));
            html.AppendLine("</div>");
        }

        var nestedTypes = type.GetNestedTypes(BindingFlags.Public);
        if (nestedTypes.Length > 0)
        {
            html.AppendLine("<div><strong>Nested types:</strong> ");
            html.AppendLine(string.Join(", ", nestedTypes.Select(FormatTypeLink)));
            html.AppendLine("</div>");
        }

        var derivedTypes = _allTypes.Where(t => t.BaseType == type).ToList();
        if (derivedTypes.Count > 0)
        {
            html.AppendLine("<div><strong>Derived types:</strong> ");
            html.AppendLine(string.Join(", ", derivedTypes.Select(FormatTypeLink)));
            html.AppendLine("</div>");
        }

        if (type.IsInterface)
        {
            var implementers = _allTypes.Where(t => t.GetInterfaces().Contains(type)).ToList();
            if (implementers.Count > 0)
            {
                html.AppendLine("<div><strong>Implemented by:</strong> ");
                html.AppendLine(string.Join(", ", implementers.Select(FormatTypeLink)));
                html.AppendLine("</div>");
            }
        }

        html.AppendLine("</div>");

        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

        if (constructors.Length > 0)
        {
            html.AppendLine("<h2>Constructors</h2>");
            foreach (var ctor in constructors)
            {
                html.AppendLine("<div class='member'>");
                html.AppendLine($"<div class='signature'>{FormatConstructor(ctor)}</div>");

                var ctorSummary = GetSummary(ctor);
                if (!string.IsNullOrWhiteSpace(ctorSummary))
                {
                    html.AppendLine($"<div class='summary'>{ctorSummary}</div>");
                }

                html.AppendLine("</div>");
            }
        }

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        if (properties.Length > 0)
        {
            html.AppendLine("<h2>Properties</h2>");

            foreach (var prop in properties.OrderBy(p => p.Name))
            {
                html.AppendLine("<div class='member'>");
                html.AppendLine($"<div class='signature'>{FormatProperty(prop)}</div>");
                var propSummary = GetSummary(prop);

                if (!string.IsNullOrWhiteSpace(propSummary))
                {
                    html.AppendLine($"<div class='summary'>{propSummary}</div>");
                }

                html.AppendLine("</div>");
            }
        }

        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .ToArray();
        if (methods.Length > 0)
        {
            html.AppendLine("<h2>Methods</h2>");

            foreach (var method in methods.OrderBy(m => m.Name))
            {
                html.AppendLine("<div class='member'>");
                html.AppendLine($"<div class='signature'>{FormatMethod(method)}</div>");

                var methodSummary = GetSummary(method);
                if (!string.IsNullOrWhiteSpace(methodSummary))
                {
                    html.AppendLine($"<div class='summary'>{methodSummary}</div>");
                }

                html.AppendLine("</div>");
            }
        }

        html.AppendLine("</body></html>");

        // Create directory structure for the namespace
        var typeFilePath = Path.Combine(_outputDir, GetTypeFileName(type));
        var typeDirectory = Path.GetDirectoryName(typeFilePath);

        if (!string.IsNullOrEmpty(typeDirectory))
        {
            Directory.CreateDirectory(typeDirectory);
        }

        File.WriteAllText(typeFilePath, html.ToString());
    }

    /// <summary>
    /// Loads XML documentation for the specified assembly and stores it for later use.
    /// </summary>
    /// <remarks>This method reads the XML documentation file associated with the specified assembly and
    /// parses its contents. The documentation for each member is stored in an internal dictionary for quick access. If
    /// the XML file does not exist or cannot be loaded, the method logs a warning and continues execution.</remarks>
    /// <param name="assemblyPath">The file path of the assembly whose XML documentation is to be loaded. The method expects an XML documentation
    /// file with the same name as the assembly and a ".xml" extension to exist in the same directory.</param>
    private void LoadXmlDocumentation(string assemblyPath)
    {
        var xmlPath = Path.ChangeExtension(assemblyPath, ".xml");

        if (File.Exists(xmlPath))
        {
            try
            {
                var xml = XDocument.Load(xmlPath);

                foreach (var member in xml.Descendants("member"))
                {
                    var name = member.Attribute("name")?.Value;

                    if (name != null)
                    {
                        _xmlDocs[name] = member;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load XML documentation: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Retrieves the summary documentation for the specified member, if available.
    /// </summary>
    /// <remarks>This method searches the XML documentation associated with the assembly for the specified
    /// member and extracts the content of the <c>&lt;summary&gt;</c> tag, if present.</remarks>
    /// <param name="member">The <see cref="MemberInfo"/> representing the member whose summary documentation is to be retrieved.</param>
    /// <returns>A string containing the summary documentation for the specified member, or <see langword="null"/> if no summary
    /// documentation is found.</returns>
    private string? GetSummary(MemberInfo member)
    {
        var key = GetXmlDocKey(member);

        if (key == null) return null;

        if (_xmlDocs.TryGetValue(key, out var doc))
        {
            var summary = doc.Element("summary")?.Value.Trim();
            return summary;
        }

        return null;
    }

    /// <summary>
    /// Generates an XML documentation key for the specified <see cref="MemberInfo"/>.
    /// </summary>
    /// <remarks>The XML documentation key is a unique identifier used to reference members in XML
    /// documentation files.  The format of the key depends on the type of the member: <list type="bullet"> <item>
    /// <description>For types: <c>T:Namespace.TypeName</c></description> </item> <item> <description>For constructors:
    /// <c>M:Namespace.TypeName.#ctor(ParameterType1,ParameterType2,...)</c></description> </item> <item>
    /// <description>For methods:
    /// <c>M:Namespace.TypeName.MethodName(ParameterType1,ParameterType2,...)</c></description> </item> <item>
    /// <description>For properties: <c>P:Namespace.TypeName.PropertyName</c></description> </item> </list> Nested types
    /// are represented with a period (<c>.</c>) instead of a plus sign (<c>+</c>).</remarks>
    /// <param name="member">The reflection metadata object representing the member for which to generate the XML documentation key. This can
    /// be a type, constructor, method, or property.</param>
    /// <returns>A string representing the XML documentation key for the specified member, or <see langword="null"/> if the
    /// member type is not supported.</returns>
    private static string? GetXmlDocKey(MemberInfo member)
    {
        if (member is Type type)
        {
            return "T:" + type.FullName?.Replace("+", ".");
        }
        else if (member is ConstructorInfo ctor)
        {
            var paramList = string.Join(",", ctor.GetParameters().Select(p => p.ParameterType.FullName));

            return $"M:{ctor.DeclaringType?.FullName}.#ctor({paramList})".Replace("+", ".");
        }
        else if (member is MethodInfo method)
        {
            var paramList = string.Join(",", method.GetParameters().Select(p => p.ParameterType.FullName));
            var key = $"M:{method.DeclaringType?.FullName}.{method.Name}";
            if (!string.IsNullOrEmpty(paramList))
            {
                key += $"({paramList})";
            }

            return key.Replace("+", ".");
        }
        else if (member is PropertyInfo prop)
        {
            return $"P:{prop.DeclaringType?.FullName}.{prop.Name}".Replace("+", ".");
        }

        return null;
    }

    /// <summary>
    /// Determines the kind of a specified <see cref="Type"/> and returns its classification as a string.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> to evaluate. Must not be <see langword="null"/>.</param>
    /// <returns>A string representing the kind of the specified type. Possible values are: "interface", "enum", "struct",
    /// "static class", "abstract class", or "class".</returns>
    private static string GetTypeKind(Type type)
    {
        if (type.IsInterface) return "interface";
        if (type.IsEnum) return "enum";
        if (type.IsValueType) return "struct";
        if (type.IsAbstract && type.IsSealed) return "static class";
        if (type.IsAbstract) return "abstract class";
        return "class";
    }

    /// <summary>
    /// Generates a file path for the specified type, using its namespace and name.
    /// </summary>
    /// <remarks>The generated path uses the type's namespace as a directory structure, replacing dots (".")
    /// with the system's directory separator character. If the type name contains a plus sign ("+"), it is replaced
    /// with an underscore ("_") in the file name.</remarks>
    /// <param name="type">The <see cref="Type"/> for which to generate the file path. Must not be <see langword="null"/>.</param>
    /// <returns>A string representing the file path, where the namespace is converted to a directory structure and the type name
    /// is used as the file name with an ".html" extension.</returns>
    private static string GetTypeFileName(Type type)
    {
        // Create path like: Namespace/Subnamespace/TypeName.html
        var namespacePath = type.Namespace?.Replace(".", Path.DirectorySeparatorChar.ToString()) ?? "";
        var fileName = type.Name.Replace("+", "_") + ".html";

        if (!string.IsNullOrEmpty(namespacePath))
        {
            return Path.Combine(namespacePath, fileName);
        }

        return fileName;
    }

    /// <summary>
    /// Generates the relative path to the root "index.html" file based on the namespace depth of the specified type.
    /// </summary>
    /// <remarks>The method calculates the number of levels in the namespace hierarchy of the specified type
    /// and constructs a relative path that navigates up to the root directory before appending "index.html".</remarks>
    /// <param name="type">The <see cref="Type"/> whose namespace depth is used to calculate the relative path.</param>
    /// <returns>A string representing the relative path to the "index.html" file. If the type has no namespace, the path is
    /// "index.html".</returns>
    private static string GetRelativeIndexPath(Type type)
    {
        // Calculate how many levels deep we are
        var depth = type.Namespace?.Split('.').Length ?? 0;

        if (depth == 0)
        {
            return "index.html";
        }

        // Go up 'depth' levels to reach the root
        var upLevels = string.Join("/", Enumerable.Repeat("..", depth));
        return $"{upLevels}/index.html";
    }

    /// <summary>
    /// Formats a hyperlink for the specified type if it exists in the collection of known types.
    /// </summary>
    /// <remarks>This method checks whether the specified type is part of a predefined collection of types. 
    /// If the type is found, it generates an HTML hyperlink pointing to the file associated with the type. Otherwise,
    /// it returns the type's name without a hyperlink.</remarks>
    /// <param name="type">The <see cref="Type"/> to format as a hyperlink.</param>
    /// <returns>A string containing an HTML anchor tag with the type's name as the link text if the type exists   in the
    /// collection of known types; otherwise, the name of the type as plain text.</returns>
    private string FormatTypeLink(Type type)
    {
        if (_allTypes.Contains(type))
        {
            return $"<a href='{GetTypeFileName(type)}'>{type.Name}</a>";
        }

        return type.Name;
    }

    /// <summary>
    /// Formats the signature of a constructor into a string representation.
    /// </summary>
    /// <remarks>The formatted string includes the name of the declaring type in bold and the parameter list
    /// with their types and names.</remarks>
    /// <param name="ctor">The <see cref="ConstructorInfo"/> object representing the constructor to format.</param>
    /// <returns>A string containing the formatted constructor signature, including the declaring type name and parameter list.</returns>
    private static string FormatConstructor(ConstructorInfo ctor)
    {
        var parameters = string.Join(", ", ctor.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));

        return $"<strong>{ctor.DeclaringType?.Name}</strong>({parameters})";
    }

    /// <summary>
    /// Formats the metadata of a property into a string representation.
    /// </summary>
    /// <param name="prop">The <see cref="PropertyInfo"/> object representing the property to format.</param>
    /// <returns>A string that includes the property's type, name, and accessors (e.g., "get;" and "set;").</returns>
    private static string FormatProperty(PropertyInfo prop)
    {
        List<string> accessors = [];
        if (prop.CanRead) accessors.Add("get;");
        if (prop.CanWrite) accessors.Add("set;");
        return $"{prop.PropertyType.Name} <strong>{prop.Name}</strong> {{ {string.Join(" ", accessors)} }}";
    }

    /// <summary>
    /// Formats the signature of a method, including its return type, name, and parameters, into a string
    /// representation.
    /// </summary>
    /// <param name="method">The <see cref="MethodInfo"/> object representing the method to format.</param>
    /// <returns>A string containing the formatted method signature, including the return type, method name, and parameter list.</returns>
    private static string FormatMethod(MethodInfo method)
    {
        var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
        return $"{method.ReturnType.Name} <strong>{method.Name}</strong>({parameters})";
    }

    /// <summary>
    /// Generates and returns a predefined CSS stylesheet as a string.
    /// </summary>
    /// <remarks>The returned stylesheet defines styles for common HTML elements such as body, headers, links,
    /// and custom classes. It is designed to provide a clean and professional appearance, suitable for documentation or
    /// web-based content.</remarks>
    /// <returns>A string containing the CSS stylesheet.</returns>
    private static string GetStyleSheet()
    {
        return @"<style>
            body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 20px; line-height: 1.6; max-width: 1200px; }
            .header { margin-bottom: 20px; }
            .header a { text-decoration: none; color: #0066cc; }
            h1 { color: #333; border-bottom: 2px solid #0066cc; padding-bottom: 10px; }
            h2 { color: #555; margin-top: 30px; border-bottom: 1px solid #ddd; padding-bottom: 5px; }
            .namespace { color: #666; font-size: 0.9em; margin-bottom: 20px; }
            .type-summary { background: #f0f8ff; padding: 15px; border-left: 4px solid #0066cc; margin: 20px 0; }
            .relationships { background: #f9f9f9; padding: 15px; border-radius: 5px; margin: 15px 0; }
            .relationships div { margin: 5px 0; }
            .member { margin: 15px 0; padding: 15px; background: #fafafa; border-radius: 5px; }
            .signature { font-family: 'Courier New', monospace; color: #333; margin-bottom: 8px; }
            .signature strong { color: #0066cc; }
            .summary { color: #555; font-size: 0.95em; margin-top: 8px; }
            .type-kind { display: inline-block; background: #e0e0e0; padding: 2px 8px; border-radius: 3px; font-size: 0.85em; margin-right: 5px; }
            a { color: #0066cc; text-decoration: none; }
            a:hover { text-decoration: underline; }
            ul { list-style-type: none; padding-left: 0; }
            li { margin: 10px 0; padding: 10px; background: #fafafa; border-radius: 3px; }
            </style>";
    }
}