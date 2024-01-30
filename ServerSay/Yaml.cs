using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

//required to access the Reference File YamlAssembly.DLL
using YamlDotNet.Serialization;


namespace ServerSay
{
    class AdminConfig
    {

        public static Root ReadYaml(String filePath)
        {
            using (var input = File.OpenText(filePath))
            {
                var deserializer = new DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .Build();
                var Output = deserializer.Deserialize<Root>(input);
                return Output;
            }
        }

        public class Root
        {
            public List<elevated> Elevated { get; set; }
        }

        public class elevated
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Permission { get; set; }
        }

        public static void WriteYaml(string Path, Root ConfigData)
        {
            File.WriteAllText(Path, "---\r\n");
            Serializer serializer = new SerializerBuilder()
                .Build();
            string WriteThis = serializer.Serialize(ConfigData);
            File.AppendAllText(Path, WriteThis);

        }
    }
}
