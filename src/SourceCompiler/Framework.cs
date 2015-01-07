using System.Xml.Serialization;
using System.IO;
using System;

namespace SourceCompiler
{
    internal static class Framework
    {
        internal static void SaveXml<T>(this T obj, string path)
        {
            var xs = new XmlSerializer(obj.GetType());
            var fs = new FileStream(path, FileMode.Create);
            var ns = new XmlSerializerNamespaces();
            ns.Add("", "");

            xs.Serialize(fs, obj, ns);
            fs.Close();
        }

        internal static T LoadXml<T>(string path)
        {
            var xs = new XmlSerializer(typeof(T));
            if (!File.Exists(path)) return default(T);
            var fs = new FileStream(path, FileMode.Open);
            var cReturn = (T)xs.Deserialize(fs);
            fs.Close();
            return cReturn;
        }

        public static bool HasFlag(this Enum variable, Enum value)
        {
            if (variable.GetType() != value.GetType())
                throw new ArgumentException("The checked flag is not from the same type as the checked variable.");
            ulong num = Convert.ToUInt64(value);
            ulong num2 = Convert.ToUInt64(variable);
            
            return (num2 & num) == num;
        }

    }
}
