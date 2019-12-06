using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
            Console.WriteLine("Generating packets...");

            var asm = Assembly.Load("Rhisis.Network");
            var packets = asm.GetTypes().Where(t =>  t.Namespace != null && 
                                                    (t.Namespace.StartsWith("Rhisis.Network.Packets.Login") ||
                                                     t.Namespace.StartsWith("Rhisis.Network.Packets.Cluster") ||
                                                     t.Namespace.StartsWith("Rhisis.Network.Packets.World"))
                                               );

            // Write server overview pages
            var loginPackets = asm.GetTypes().Where(t =>
                t.Namespace != null && t.Namespace.StartsWith("Rhisis.Network.Packets.Login"));
            var clusterPackets = asm.GetTypes().Where(t =>
                t.Namespace != null && t.Namespace.StartsWith("Rhisis.Network.Packets.Cluster"));
            var worldPackets = asm.GetTypes().Where(t =>
                t.Namespace != null && t.Namespace.StartsWith("Rhisis.Network.Packets.World"));
            GenerateOverviewPage(loginPackets);
            GenerateOverviewPage(clusterPackets);
            GenerateOverviewPage(worldPackets);

            // Write all packet pages
            var sr = new StreamReader("Rhisis.Network.xml");
            var xml = new XmlDocument();
            xml.Load(sr);
            sr.Close();
            foreach (var packet in packets)
                PacketClassToMarkupPage(packet, xml);

            Console.WriteLine("Generated all found packets.");
        }

        private static void GenerateOverviewPage(IEnumerable<Type> packets)
        {
            var sb = new StringBuilder();

            var server = packets.First().Namespace.Split(".").Skip(3).Take(1).First();

            sb.AppendLine($"[[Packets|Packets]] / [[{server}|{server}]]");

            sb.AppendLine("## Overview");
            foreach (var packet in packets)
                sb.AppendLine($"[[{packet.Name}|{packet.Name}]]{Environment.NewLine}");

            var packetPath = "Packets";
            if (!Directory.Exists(packetPath))
                Directory.CreateDirectory(packetPath);

            using (var sw = new StreamWriter($"{packetPath}/{server}.md"))
                sw.Write(sb.ToString());
        }

        private static void PacketClassToMarkupPage(Type type, XmlDocument xml)
        {
            var sb = new StringBuilder();

            // Prepare meta informations
            var server = type.Namespace.Split(".").Skip(3).Take(1).First();
            var className = type.Name;

            // Create directories
            var packetPath = "Packets";
            var serverPath = $"{packetPath}/{server}";
            if (!Directory.Exists(packetPath))
                Directory.CreateDirectory(packetPath);
            if (!Directory.Exists(serverPath))
                Directory.CreateDirectory(serverPath);

            sb.AppendLine($"[[Packets|Packets]] / [[{server}|{server}]] / [[{className}|{className}]]");
            sb.AppendLine($"## Packet Structure");
            sb.AppendLine("Type | Name | Summary");
            sb.AppendLine("--- | --- | ---");

            foreach (var property in type.GetProperties())
                sb.AppendLine(PropertyToText(property, sb, xml));

            using (var sw = new StreamWriter($"{serverPath}/{className}.md"))
                sw.Write(sb.ToString());
        }

        private static string PropertyToText(PropertyInfo property, StringBuilder sb, XmlDocument xml)
        {
            // Get summary
            var propertySummary = xml["doc"]["members"].SelectSingleNode("member[@name='P:" + $"{property.DeclaringType.FullName}.{property.Name}" + "']")?.SelectSingleNode("summary").InnerText.Trim();
            if (propertySummary is null)
                propertySummary = "(Empty)";

            return $"{GetPropertyBaseTypeString(property.PropertyType)} | {property.Name} | {propertySummary}";
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
                // Class Vector3 has two calculated properties that are not part of the packet structure
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
