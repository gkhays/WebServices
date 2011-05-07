using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Soap;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Web.Services.Protocols;

using NUnit.Framework;
using GKH.Web.Service;

namespace GKH.Web.Service
{
    [TestFixture]
    class TestWSDynamicProxy
    {
        private string uriString = string.Empty;
        private WSDynamicProxy proxy = null;
        private List<string> serviceList = null;

        [SetUp]
        public void SetUp()
        {
            this.uriString = ConfigurationManager.AppSettings["URI"];
            UriBuilder uriBuilder = new UriBuilder(uriString);
            this.proxy = new WSDynamicProxy(uriBuilder.Uri);
            this.serviceList = proxy.Services;
        }

        [Test]
        [ExpectedException(typeof(Exception), 
            ExpectedMessage = "Error compiling assembly from WSDL")]
        public void TestConstructionWithInvalidUri()
        {
            WSDynamicProxy proxy = new WSDynamicProxy(new UriBuilder("bad").Uri);
        }

        [Test]
        public void TestEnumerateServices()
        {
            Assert.Greater(this.serviceList.Count, 0);

            foreach (string serviceName in this.serviceList)
                Console.WriteLine(serviceName);
        }

        [Test]
        public void TestEnumerateMethods()
        {
            int count = 0;
            foreach (string serviceName in this.serviceList)
            {
                List<string> methodList = this.proxy.EnumerateMethods(serviceName);
                // TODO - Some services don't have any methods. So this is a visual
                // test for now. Maybe Assert.DoesNotThrow()?
                //Assert.Greater(methodList.Count, 0);

                foreach (string methodName in methodList)
                {
                    Console.WriteLine(methodName);
                    count++;
                }
            }

            // If we accumulate any methods, consider it a success.
            Assert.Greater(count, 0);
        }

        [Test]
        public void TestEnumerateMethodsAndParameters()
        {
            Assert.Greater(this.serviceList.Count, 0);

            int methodCount = 0;
            int parameterCount = 0;
            foreach (string serviceName in this.serviceList)
            {
                Console.WriteLine(serviceName);
                List<string> methodList = proxy.EnumerateMethods(serviceName);
                methodCount += methodList.Count;
                
                foreach (string methodName in methodList)
                {
                    Console.Write("  {0}(", methodName);
                    int count = 0; // Reset the parameter count for comma placement.
                    Dictionary<string, string> parameters = 
                        proxy.EnumerateMethodParameters(serviceName, methodName);
                    foreach (KeyValuePair<string, string> pair in parameters)
                    {
                        Console.Write("{0} {1}", pair.Value, pair.Key);
                        if (count + 1 < parameters.Count)
                            Console.Write(", ");
                        count++;
                        parameterCount += count;
                    }
                    Console.WriteLine(")");
                }
            }

            // As above, any amount of methods is OK. We'll take any amount of
            // parameters too.
            Assert.Greater(methodCount, 0);
            Assert.Greater(parameterCount, 0);
            Console.WriteLine("Summary: {0} total methods, {1} total parameters", 
                methodCount, parameterCount);
        }

        //public static string SerializeObjectToXML(T obj)
        public static string SerializeObjectToXML(Object obj)
        {
            try
            {
                //string xmlString = null;
                MemoryStream memoryStream = new MemoryStream();
                XmlSerializer xs = new XmlSerializer(obj.GetType());
                string s = string.Empty;
                XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8);
                SoapFormatter xsoap = new SoapFormatter();
                xsoap.Serialize(memoryStream, obj);

                //xmlString = UTF8ByteArrayToString(memoryStream.ToArray());
                UnicodeEncoding encoding = new UnicodeEncoding();
                byte[] bytes = memoryStream.ToArray();
                char[] chars = new char[encoding.GetCharCount(bytes, 0, (int)memoryStream.Length)];
                return new string(chars);
            }
            catch (Exception ex)
            {
                ex.GetType();
                return string.Empty;
            }
        }

    //    public static T UnserializeObjectFromXML(string xml)
    //    {
    //        XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(T));
    //        MemoryStream memoryStream = new MemoryStream(StringToUTF8ByteArray(xml));
    //        XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8);
    //        return (T)xs.Deserialize(memoryStream);
    //    }
    }
}
