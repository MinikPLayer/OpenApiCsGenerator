using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenApiCsGenerator
{
    internal class Program
    {
        static string GenerateCode(PathsTreeNode root)
        {
            var baseNode = root.GetByPath("api");
            if (baseNode == null)
                baseNode = root;

            string ret = "";
            foreach(var node in baseNode)
            {
                ret += $"public class Api_{node.Name}\n";
                ret += "{\n";
                foreach (var method in node)
                    if (method.IsEndpoint)
                        ret += "\t" + method.ToString() + "\n";

                ret += "}\n\n";
            }

            return ret;
        }

        static void Main(string[] args)
        {
            string file = "example.json";

            var data = File.ReadAllText(file);
            var json = JsonConvert.DeserializeObject(data) as JObject;
            if (json == null)
                throw new FileLoadException("File parsing error - invalid file");

            var version = json["openapi"];
            Console.WriteLine("OpenApi version: " + version);

            var title = json["info"]["title"];
            Console.WriteLine("Title: " + title);

            var paths = json["paths"];
            if (paths == null)
                throw new InvalidDataException("No paths in data");

            var treeRoot = new PathsTreeNode("root", "");
            foreach (var child in paths.Children().OfType<JProperty>())
            {
                var name = child.Name.TrimStart('/');
                treeRoot.Add(name, child);
            }

            Console.WriteLine(treeRoot.Print());

            Console.WriteLine("\n\n\n");
            var code = GenerateCode(treeRoot);
            Console.WriteLine(code);
        }
    }
}