/*
 *  Copyright © 2016 Nestorium
 *

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 *
 * */

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace DS2POCO
{
    /// <summary>
    /// Processor class for WCF Data Services
    /// </summary>
    /// <remarks>It generates pure POCO classes based on data service metadata</remarks>
    public class DS2POCOProcessor
    {
        //temporary workspace
        private static AdhocWorkspace workspace;

        //Roslyn code generator
        private static SyntaxGenerator generator;

        //node elements of the class-to-be-generated
        private static List<SyntaxNode> nodeElements;

        //callback delegate method
        private static Action<string> callbackDelegate;

        //dictionary for dataservice-domain types
        private static Dictionary<string, string> types;

        //static ctor
        static DS2POCOProcessor()
        {
            types = new Dictionary<string, string>();
            types.Add("Edm.Guid", "Guid");
            types.Add("Edm.DateTime", "DateTime");
            types.Add("Edm.String", "string");
            types.Add("Edm.Int32", "int");
            types.Add("Edm.Boolean", "bool");
        }

        /// <summary>
        /// Asynchronous WCF Data Service processing.
        /// </summary>
        /// <param name="uri">URI of DataService metadata.</param>
        /// <param name="exportDirectory">Directory for the output files.</param>
        /// <param name="primaryNamespace">Basic namespace (default value: Proxy).</param>
        /// <param name="baseClassName">Base class name (optional).</param>
        /// <param name="usingNamespaces">Using namespaces (optional).</param>
        /// <param name="callback">Callback delegate (optional).</param>
        /// <remarks>When using a Data Service it is important to use $metadata query parameter!</remarks>
        /// <returns></returns>

        public async static Task Processing(string uri, string exportDirectory, string primaryNamespace = "Proxy", string baseClassName = null, string usingNamespaces = null, Action<string> callback = null)
        {
            await Task.Factory.StartNew(() =>
            {
                try
                {
                    if (callback != null)
                        callbackDelegate = callback;
                    nodeElements = new List<SyntaxNode>();
                    XmlDocument doc = new XmlDocument();
                    //try to load the URI
                    doc.Load(uri);
                    Feedback("Downloading metadata: " + uri);
                    XmlNamespaceManager xmlnsManager = new XmlNamespaceManager(doc.NameTable);
                    //necessary namespaces
                    xmlnsManager.AddNamespace("edmx", "http://schemas.microsoft.com/ado/2007/06/edmx");
                    xmlnsManager.AddNamespace("m", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");
                    //finding the root node
                    var dataServicesNode = doc.SelectSingleNode("/edmx:Edmx/edmx:DataServices", xmlnsManager);
                    Feedback("Processing...");
                    InitGenerator();
                    foreach (var child in dataServicesNode.ChildNodes)
                    {
                        string className = null;
                        //clearing
                        nodeElements.Clear();
                        //the first level children should be named 'Schema'
                        if (child is XmlElement)
                        {
                            XmlElement element = child as XmlElement;
                            foreach (var childNode in element.ChildNodes)
                            {
                                //the second level children should be named 'EntityType' - these are the basis for our classes
                                if (childNode is XmlElement)
                                {
                                    XmlElement entityTypeNode = childNode as XmlElement;
                                    //check for attributes
                                    if (entityTypeNode.Attributes.Count > 0 && entityTypeNode.Name.SameString("EntityType"))
                                        className = entityTypeNode.Attributes["Name"]?.Value;
                                    foreach (var entityChildNode in entityTypeNode.ChildNodes)
                                    {
                                        //amongs the third level, there should be nodes named 'Property'
                                        if (entityChildNode is XmlElement)
                                        {
                                            XmlElement propertyNode = entityChildNode as XmlElement;
                                            //check for attributes
                                            if (propertyNode.Attributes.Count > 0 && propertyNode.Name.SameString("Property"))
                                                //we create properties here
                                                CreateAutoImplementedProperty(propertyNode.Attributes["Name"]?.Value, propertyNode.Attributes["Type"]?.Value);
                                        }
                                    }
                                }
                                //after we collected the properties, we assemble our class
                                if (!string.IsNullOrEmpty(className))
                                    CreateOutputFile(className + ".cs", CreateClass(className, primaryNamespace, baseClassName, usingNamespaces), exportDirectory);
                            }
                        }
                    }
                    Feedback("Processing done");
                }
                catch (Exception ex)
                {
                    Feedback("An error occured while processing: " + ex.Message);
                }
            });
        }

        private static void CreateOutputFile(string fileName, SyntaxNode resultNodes, string exportDirectory)
        {
            Feedback("Creating file: " + fileName);
            if (!Directory.Exists(exportDirectory))
                Directory.CreateDirectory(exportDirectory);
            File.WriteAllText(Path.Combine(exportDirectory, fileName), resultNodes.ToString());
        }

        private static void InitGenerator()
        {
            workspace = new AdhocWorkspace();
            generator = SyntaxGenerator.GetGenerator(workspace, LanguageNames.CSharp);
        }

        /// <summary>
        /// Creating class based upon the incoming parameters
        /// </summary>
        /// <param name="className">Name of the class</param>
        /// <param name="primaryNamespace">Namespace</param>
        /// <param name="baseClassName">Name of the base class</param>
        /// <param name="usingNamespaces">Using namespaces</param>
        /// <returns></returns>
        private static SyntaxNode CreateClass(string className, string primaryNamespace, string baseClassName, string usingNamespaces)
        {
            Feedback("Creating class: " + className);
            List<SyntaxNode> syntaxNodeElements = new List<SyntaxNode>();
            syntaxNodeElements.Add(generator.NamespaceImportDeclaration("System"));
            //using namespaces
            //they are separated by newline constants - feel free to modify this!
            string[] usingNamespacesElements = usingNamespaces.Split(Environment.NewLine.ToCharArray());

            if (usingNamespacesElements.Length > 0)
                foreach (var actualElement in usingNamespacesElements)
                {
                    if (string.IsNullOrEmpty(actualElement) || actualElement.SameString("System"))
                        continue;
                    syntaxNodeElements.Add(generator.NamespaceImportDeclaration(actualElement));
                }

            //base class
            var baseClassDefinition = !string.IsNullOrEmpty(baseClassName) ? SyntaxFactory.IdentifierName(baseClassName) : null;

            //class
            var classDefinition = generator.ClassDeclaration(className, typeParameters: null, accessibility: Accessibility.Public, baseType: baseClassDefinition, members: nodeElements);
            generator.AddBaseType(classDefinition, SyntaxFactory.IdentifierName(baseClassName));
            //combining the class and the primary namespace
            var namespaceDeclaration = generator.NamespaceDeclaration(primaryNamespace, classDefinition);
            syntaxNodeElements.Add(namespaceDeclaration);
            //you should always normalize the whitespaces - otherwise you get a mess
            return generator.CompilationUnit(syntaxNodeElements).NormalizeWhitespace();
        }

        private static void CreateAutoImplementedProperty(string propertyName, string typeName)
        {
            string actualType = null;
            if (types.TryGetValue(typeName, out actualType))
            {
                var property = SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(types[typeName]), propertyName).AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                               .AddAccessorListAccessors(
                                      SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                                      SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
                Feedback("Creating property: " + propertyName);
                nodeElements.Add(property);
            }
        }

        private static void Feedback(string message)
        {
            callbackDelegate?.Invoke(string.Format("{0} - {1}", DateTime.UtcNow.ToString("yyyy.MM.dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture), message));
        }
    }

    /// <summary>
    /// Extension methods for various operations
    /// </summary>
    internal static class Extensions
    {
        /// <summary>
        /// Checks if the two string has the same value regardless the casing.
        /// </summary>
        /// <param name="baseString">The string to compare.</param>
        /// <param name="otherString">The string to compare to.</param>
        /// <returns></returns>
        public static bool SameString(this string baseString, string otherString)
        {
            return string.IsNullOrEmpty(baseString) || string.IsNullOrEmpty(otherString)
                ? false
                : baseString.Equals(otherString, StringComparison.OrdinalIgnoreCase);
        }
    }
}