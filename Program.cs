using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net;
using System.Text;

namespace OpenApiCsGenerator
{
    internal class Program
    {
        public static string DataStructurePrefix = "ApiTypes";
        public static string ApiControllersPrefix = "Api";

        static string GenerateCode(PathsTreeNode root)
        {
            var baseNode = root.GetByPath("api");
            if (baseNode == null)
                baseNode = root;

            StringBuilder ret = new StringBuilder();
            foreach(var node in baseNode)
            {
                ret.Append($"public static class {ApiControllersPrefix}{node.Name}\n");
                ret.Append("{\n");
                if (node.IsEndpoint)
                {
                    ret.Append("\t" + node.ToString(node.Type.ToString()) + "\n");
                }
                else
                {
                    foreach (var method in node)
                        if (method.IsEndpoint)
                            ret.Append("\t" + method.ToString() + "\n");
                }

                ret.Append("}\n\n");
            }

            return ret.ToString();
        }

        static string GenerateTypesCode(JObject json)
        {
            var components = json["components"] as JObject;
            if (components == null)
                return "";

            var builder = new StringBuilder();
            builder.Append("namespace ");
            builder.Append(DataStructurePrefix);
            builder.Append("\n{ \n");

            var enumsProperty = components.Descendants().OfType<JProperty>().Where(x => x.Name == "enum");
            foreach(var e in enumsProperty)
            {
                if (e.First is not JArray arr || e.Parent is not JObject pObj || pObj.Parent is not JProperty parentProp)
                    continue;

                var name = parentProp.Name;
                builder.Append($"\tpublic enum {name}\n");
                builder.Append("\t{\n");
                foreach(var v in arr)
                {
                    builder.Append("\t\t");
                    builder.Append(name);
                    builder.Append(v.ToString());
                    builder.Append(" = ");
                    builder.Append(v.ToString());
                    builder.Append(",\n");
                }
                builder.Append("\t}\n\n");
            }

            var properties = components.Descendants().OfType<JProperty>().Where(x => x.Name == "properties");
            foreach (var prop in properties)
            {
                if (prop.First is not JObject ob || prop.Parent is not JObject pObj || pObj.Parent is not JProperty parentProp)
                    continue;

                var typeName = parentProp.Name;
                var children = ob.Children();
                builder.Append($"\n\tpublic struct {typeName}\n");
                builder.Append("\t{\n");
                foreach (var c in children.OfType<JProperty>())
                {
                    var type = PathsTreeNode.DecodeTypeName(c.Descendants());
                    var name = c.Name.FirstLetterToUpper();
                    builder.Append("\t\tpublic ");
                    builder.Append(type);
                    builder.Append(" ");
                    builder.Append(name);
                    builder.Append(" { get; set; }\n");
                }
                builder.Append("\t}\n");
            }


            builder.Append("}\n\n");
            return builder.ToString();
        }

        static void Main(string[] args)
        {
            string? filePath;
            if (args.Length > 0)
            {
                filePath = args[0];
            }
            else
            {
                Console.Write("Enter file path or url starting with http:// or https://: ");
                filePath = Console.ReadLine();
                if (filePath == null)
                    return;
            }

            string? data;
            if (filePath.StartsWith("http://") || filePath.StartsWith("https://"))
            {
                data = new HttpClient().GetStringAsync(filePath).Result;
            }
            else
            {
                data = File.ReadAllText(filePath);
            }

            if (data == null)
                throw new Exception("Cannot load data");

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

            var typesCode = GenerateTypesCode(json);
            var code = GenerateCode(treeRoot);

            var allCode = typesCode + code;
            TextCopy.ClipboardService.SetText(allCode);
            Console.WriteLine(allCode);
            Console.WriteLine("Copied to clipboard!");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
    }
}