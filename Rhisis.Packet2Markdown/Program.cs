using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;

namespace Rhisis.Packet2Markdown
{
    class Program
    {
        private static readonly Dictionary<Type, string> Aliases = new Dictionary<Type, string>()
        {
            { typeof(byte), "byte" },
            { typeof(sbyte), "sbyte" },
            { typeof(short), "short" },
            { typeof(ushort), "ushort" },
            { typeof(int), "int" },
            { typeof(uint), "uint" },
            { typeof(long), "long" },
            { typeof(ulong), "ulong" },
            { typeof(float), "float" },
            { typeof(double), "double" },
            { typeof(decimal), "decimal" },
            { typeof(object), "object" },
            { typeof(bool), "bool" },
            { typeof(char), "char" },
            { typeof(string), "string" },
            { typeof(void), "void" },
            { typeof(byte?), "byte?" },
            { typeof(sbyte?), "sbyte?" },
            { typeof(short?), "short?" },
            { typeof(ushort?), "ushort?" },
            { typeof(int?), "int?" },
            { typeof(uint?), "uint?" },
            { typeof(long?), "long?" },
            { typeof(ulong?), "ulong?" },
            { typeof(float?), "float?" },
            { typeof(double?), "double?" },
            { typeof(decimal?), "decimal?" },
            { typeof(bool?), "bool?" },
            { typeof(char?), "char?" }
        };

        static void Main(string[] args)
        {
            if (args.Length < 1)
                throw new ArgumentNullException($"Please enter the following arguments: {Environment.NewLine}" +
                                                         "[0] Path to Rhisis.Network assembly file." +
                                                         "[1] Path tho Rhisis.Network.xml (generated Visual Studio documentation file)");

            if (!File.Exists(args[0]))
                throw new FileNotFoundException("Couldn't find Rhisis.Network assembly file.");

            if (!File.Exists(args[1]))
                throw new FileNotFoundException("Couldn't find Rhisis.Network.xml.'");

            Console.WriteLine("Loading Assembly Rhisis.Network...");
            var asm = Assembly.LoadFile(args[0]);

            Console.WriteLine("Getting all types in Namespace Rhisis.Network.Packets.(Login/Cluster/World)...");
            var loginPackets = asm.GetTypes().Where(t =>
                t.Namespace != null && t.Namespace.StartsWith("Rhisis.Network.Packets.Login")).OrderBy(t => t.Name);
            var clusterPackets = asm.GetTypes().Where(t =>
                t.Namespace != null && t.Namespace.StartsWith("Rhisis.Network.Packets.Cluster")).OrderBy(t => t.Name);
            var worldPackets = asm.GetTypes().Where(t =>
                t.Namespace != null && t.Namespace.StartsWith("Rhisis.Network.Packets.World")).OrderBy(t => t.Name);

            Console.WriteLine("Generating pages...");
            GeneratePage(loginPackets, args[1]);
            GeneratePage(clusterPackets, args[1]);
            GeneratePage(worldPackets, args[1]);

            Console.WriteLine("Finished page generation!");
        }

        private static void GeneratePage(IEnumerable<Type> packets, string xmlPath)
        {
            var pageBase = File.ReadAllText(@"PageTemplate.txt");
            var packetBase = File.ReadAllText(@"PacketTemplate.txt");

            var server = packets.First().Namespace.Split(".").Skip(3).Take(1).First();

            var page = pageBase.Replace("{ServerName}", server);

            var packetOverview = new StringBuilder();

            var xml = new XmlDocument();
            using (var sr = new StreamReader(xmlPath))
                xml.Load(sr);

            foreach (var packet in packets)
                packetOverview.AppendLine(GetPacketTableRowText(xml, packet));

            page = page.Replace("{PacketOverviewContent}", packetOverview.ToString());

            var packetContent = new StringBuilder();

            foreach (var packet in packets)
                packetContent.AppendLine(GetPacketDocumentation(xml, packet, packetBase));

            page = page.Replace("{PacketContent}", packetContent.ToString());

            using (var sw = new StreamWriter($"{server}.md"))
                sw.Write(page);
        }

        private static string GetClassSummary(XmlDocument xml, Type packet)
        {
            var classSummary = xml["doc"]["members"].SelectSingleNode("member[@name='T:" + $"{packet.FullName}" + "']")?.SelectSingleNode("summary").InnerXml.Trim();
            if (classSummary is null)
                classSummary = "(Empty)";
            else
            {
                var tempXml = new XmlDocument();
                tempXml.LoadXml($"<root>{classSummary}</root>");
                var tempSummary = new StringBuilder();
                foreach (XmlNode node in tempXml.ChildNodes)
                {
                    foreach (XmlNode childNode in node.ChildNodes)
                    {
                        var innerText = childNode.InnerText;
                        if (!string.IsNullOrEmpty(innerText))
                            tempSummary.Append(innerText);
                        else
                        {
                            var attributes = childNode.Attributes;
                            if (attributes.Count >= 1)
                            {
                                var attributeValue = attributes[0].Value;
                                if (!string.IsNullOrWhiteSpace(attributeValue))
                                {
                                    var className = attributeValue.Split('.').LastOrDefault();
                                    if (className != null)
                                        tempSummary.Append(className);
                                }
                            }
                        }
                    }
                }
                classSummary = tempSummary.ToString();
            }

            return classSummary;
        }

        private static string GetPacketTableRowText(XmlDocument xml, Type packet)
        {
            return $"| [{packet.Name}](#{packet.Name.ToLower().Replace(' ', '-')}) | {GetClassSummary(xml, packet)} |";
        }

        private static string GetPacketDocumentation(XmlDocument xml, Type packet, string template)
        {
            var doc = template.Replace("{PacketName}", $"{packet.Name}");
            doc = doc.Replace("{Description}", GetClassSummary(xml, packet));

            var tempProperties = new StringBuilder();
            foreach (var property in packet.GetProperties())
                tempProperties.AppendLine(GetPacketPropertyTableRowText(xml, property));

            doc = doc.Replace("{PacketProperties}", tempProperties.ToString());

            return doc;
        }

        private static string GetPacketPropertyTableRowText(XmlDocument xml, PropertyInfo property)
        {
            // Get summary
            var propertySummary = xml["doc"]["members"].SelectSingleNode("member[@name='P:" + $"{property.DeclaringType.FullName}.{property.Name}" + "']")?.SelectSingleNode("summary").InnerText.Trim();
            if (propertySummary is null)
                propertySummary = "(Empty)";
            return $"| {GetPropertyBaseTypeString(property.PropertyType)} | {property.Name} | {propertySummary} |";
        }

        private static string GetPropertyBaseTypeString(Type propertyType)
        {
            // Base Type
            if (Aliases.ContainsKey(propertyType))
                return Aliases[propertyType];

            // Enums, they always have a underlying base type
            if (propertyType.IsEnum)
                return Aliases[Enum.GetUnderlyingType(propertyType)];

            if (propertyType.IsGenericType)
            {
                var genericArguments = propertyType.GetGenericArguments();
                var genericTypes = new StringBuilder();

                foreach (var argument in genericArguments)
                {
                    genericTypes.Append(GetPropertyBaseTypeString(argument));
                    if (argument != genericArguments.Last())
                        genericTypes.Append(", ");
                }
                return $"{propertyType.Name}<{genericTypes}>";
            }

            if (propertyType.IsClass)
            {
                // Edge case: Class Vector3 has two calculated properties that are not part of the packet structure
                if (propertyType.Name.Equals("Vector3"))
                    return $"{propertyType.Name}<float, float, float>";

                var classProperties = propertyType.GetProperties();
                var propertyTypes = new StringBuilder();
                foreach (var property in classProperties)
                {
                    propertyTypes.Append(GetPropertyBaseTypeString(property.PropertyType));
                    if (property != classProperties.Last())
                        propertyTypes.Append(", ");
                }
                return $"{propertyType.Name}<{propertyTypes}>";
            }
            return propertyType.Name;
        }
    }
}
