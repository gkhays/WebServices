using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Web.Services;
using System.Web.Services.Description;
using System.Web.Services.Discovery;
using System.Xml;

namespace GKH.Web.Service
{
    /// <summary>
    /// Helper class to discover web service methods and parameters. May also
    /// execute a given web service method.
    /// </summary>
    public class WSDynamicProxy
    {
        private Dictionary<string, Type> availableTypes;
        private Assembly webServiceProxyAssembly;
        private List<string> services;
        private Uri webServiceUri;

        /// <summary>
        /// List of services in this web service.
        /// </summary>
        public List<string> Services
        {
            get { return this.services; }
        }

        /// <summary>
        /// Constructs the proxy given a specified web service URI.
        /// </summary>
        /// <param name="webServiceUri"></param>
        public WSDynamicProxy(Uri webServiceUri)
        {
            this.webServiceUri = webServiceUri;
            Initialize();
        }

        /// <summary>
        /// Gets a list of all methods available for the specified service.
        /// </summary>
        /// <param name="serviceName"></param>
        /// <returns></returns>
        public List<string> EnumerateMethods(string serviceName)
        {
            List<string> methods = new List<string>();
            if (true == this.availableTypes.ContainsKey(serviceName))
            {
                Type type = this.availableTypes[serviceName];

                // Just find methods of our generated proxy type; suppress 
                // inherited member sof SoapHttpClientProtocol.
                foreach (MethodInfo minfo in type.GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.DeclaredOnly))
                {
                    methods.Add(minfo.Name);
                }

                return methods;
            }
            else
            {
                throw new Exception("Service Not Available");
            }
        }

        /// <summary>
        /// Given a web service name and method, returns a map of the 
        /// parameters and parameter types.
        /// </summary>
        /// <param name="serviceName">The name of the web service</param>
        /// <param name="methodName">The name of the web service method</param>
        /// <returns>A map of parameters and their data types</returns>
        public Dictionary<string, string> EnumerateMethodParameters(
            string serviceName, 
            string methodName)
        {
            Dictionary<string, string> parametersAndTypes = new Dictionary<string, string>();
            List<string> methodList = EnumerateMethods(serviceName);
            string match = methodList.Find(
                delegate(string name) { return name.Equals(methodName); });
            if (false == string.IsNullOrEmpty(match))
            {
                if (this.availableTypes.ContainsKey(serviceName))
                {
                    Type t = this.availableTypes[serviceName];
                    BindingFlags flags = BindingFlags.Instance | 
                        BindingFlags.Public | BindingFlags.DeclaredOnly;
                    MethodInfo mi = t.GetMethod(methodName, flags);
                    ParameterInfo[] pi = mi.GetParameters();
                    foreach (ParameterInfo pInfo in pi)
                        parametersAndTypes.Add(
                            pInfo.Name, pInfo.ParameterType.Name);
                }
            }
            else
                throw new Exception(string.Format(
                    "Parameters not available for {0}", serviceName));

            return parametersAndTypes;
        }

        /// <summary>
        /// Invokes the specifed method of a given web service.
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="serviceName">Name of web service</param>
        /// <param name="methodName">Name of web service method</param>
        /// <param name="args">Method parameters</param>
        /// <returns></returns>
        public T InvokeMethod<T>(
            string serviceName, 
            string methodName, 
            params object[] args)
        {
            object obj = this.webServiceProxyAssembly.CreateInstance(serviceName);
            Type type = obj.GetType();

            return (T)type.InvokeMember(
                methodName, BindingFlags.InvokeMethod, null, obj, args);
        }

        // Override default constructor.
        private WSDynamicProxy()
        {
        }

        /// <summary>
        /// Compiles an assembly from the proxy class provided by the 
        /// ServiceDescriptionImporter.
        /// </summary>
        /// <param name="descriptionImporter"></param>
        /// <returns>An assembly that can be used to execute the web service 
        /// methods.</returns>
        private Assembly CompileAssembly(ServiceDescriptionImporter importer)
        {
            CodeNamespace codeNamespace = new CodeNamespace();
            CodeCompileUnit codeUnit = new CodeCompileUnit();

            codeUnit.Namespaces.Add(codeNamespace);

            ServiceDescriptionImportWarnings importWarnings = importer.Import(
                codeNamespace, codeUnit);

            if (importWarnings == 0)
            {
                CodeDomProvider compiler = CodeDomProvider.CreateProvider("CSharp");
                string[] references = new string[2] { 
                    "System.Web.Services.dll", "System.Xml.dll" };

                CompilerParameters parameters = new CompilerParameters(references);
                CompilerResults results = compiler.CompileAssemblyFromDom(
                    parameters, codeUnit);

                foreach (CompilerError ce in results.Errors)
                {
                    throw new Exception(string.Format(
                        "Compilation Error Creating Assembly: {0}", ce.ErrorText));
                }

                return results.CompiledAssembly;
            }
            else
            {
                throw new Exception("Invalid WSDL");
            }
        }

        /// <summary>
        /// Builds an assembly from a web service description.
        /// The assembly can be used to execute the web service methods.
        /// </summary>
        /// <param name="webServiceUri">Location of WSDL.</param>
        /// <returns>A web service assembly.</returns>
        private Assembly CompileAssemblyFromWSDL(Uri webServiceUri)
        {
            try
            {
                if (String.IsNullOrEmpty(webServiceUri.ToString()))
                    throw new Exception("Web Service Not Found");

                XmlTextReader xmlreader = new XmlTextReader(webServiceUri.ToString());
                ServiceDescriptionImporter descriptionImporter = ImportWsdl(xmlreader);
                return CompileAssembly(descriptionImporter);
            }
            catch (Exception e)
            {
                throw new Exception("Error compiling assembly from WSDL", e);
            }
        }

        /// <summary>
        /// Imports the WSDL into a web service description importer, which may
        /// then be used to generate a proxy class.
        /// </summary>
        /// <param name="xmlreader">The WSDL content, described by XML.</param>
        /// <returns>A ServiceDescriptionImporter that can be used to create a 
        /// proxy class.</returns>
        private ServiceDescriptionImporter ImportWsdl(XmlTextReader xmlreader)
        {
            if (false == ServiceDescription.CanRead(xmlreader))
                throw new Exception("Invalid Web Service Description");
            ServiceDescription serviceDescription = ServiceDescription.Read(xmlreader);
            ServiceDescriptionImporter descriptionImporter = new ServiceDescriptionImporter();
            descriptionImporter.ProtocolName = "Soap";
            descriptionImporter.AddServiceDescription(serviceDescription, null, null);
            descriptionImporter.Style = ServiceDescriptionImportStyle.Client;
            descriptionImporter.CodeGenerationOptions = System.Xml.Serialization.CodeGenerationOptions.GenerateProperties;

            return descriptionImporter;
        }

        private void Initialize()
        {
            this.services = new List<string>();
            this.availableTypes = new Dictionary<string, Type>();
            this.webServiceProxyAssembly = CompileAssemblyFromWSDL(this.webServiceUri);

            Type[] types = this.webServiceProxyAssembly.GetExportedTypes();

            foreach (Type type in types)
            {
                services.Add(type.FullName);
                availableTypes.Add(type.FullName, type);
            }
        }
    }
}