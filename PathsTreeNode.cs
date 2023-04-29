using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OpenApiCsGenerator
{
    public enum ApiTypes
    {
        Get,
        Post,
        Delete
    }

    internal class PathsTreeNode : List<PathsTreeNode>
    {

        public class Parameter
        {

            [JsonProperty("name")]
            public string Name { get; set; } = "";

            [JsonProperty("in")]
            public string Location { get; set; } = "query";

            [JsonProperty("style")]
            public string Style { get; set; } = "form";

            public string Type { get; set; } = "void";

            [JsonProperty("schema")]
            public JObject Schema
            {
                set => Type = DecodeTypeName(value.Descendants());
            }
        }

        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public bool IsEndpoint = false;

        public List<Parameter> Parameters = new List<Parameter>();
        public string ReturnType = "object";
        public ApiTypes Type = ApiTypes.Get;

        public static string DecodeTypeName(IEnumerable<JToken> schemaDescendants)
        {
            var plainTypes = schemaDescendants.OfType<JProperty>().Where(x => x.Name == "type").FirstOrDefault();
            bool array = false;
            if (plainTypes != null)
            {
                var value = plainTypes.Descendants().OfType<JValue>().FirstOrDefault();
                if (value == null)
                    throw new Exception("Type with no value specified");

                var name = value.Value!.ToString();
                if (name == null)
                    throw new Exception("Value null");

                switch(name)
                {
                    case "integer":
                        var formatType = schemaDescendants.OfType<JProperty>().Where(x => x.Name == "format").FirstOrDefault();
                        if(formatType != null)
                        {
                            var formatValue = formatType.Descendants().OfType<JValue>().FirstOrDefault();
                            if (formatValue == null)
                                throw new Exception("Format type with no value specified");

                            var formatName = formatValue.Value!.ToString();
                            if (formatName == null)
                                throw new Exception("Format value null");

                            switch(formatName)
                            {
                                case "int32":
                                    return "int";
                                case "int64":
                                    return "long";
                                default:
                                    throw new Exception("Unknown format type " + formatName);
                            }
                        }
                        else
                        {
                            return "int";
                        }

                    case "boolean":
                        return "bool";

                    case "array":
                        array = true;
                        break;

                    default:
                        return name;
                }

                array = true;
            }

            var customTypes = schemaDescendants.OfType<JProperty>().Where(x => x.Name == "$ref").FirstOrDefault();
            if (customTypes == null)
                throw new Exception("No custom or plain type specified");

            var customValue = customTypes.Descendants().OfType<JValue>().First().ToString();
            var typeName = $"{Program.DataStructurePrefix}.{customValue.Split('/').Last()}";
            if (array)
                typeName = $"List<{typeName}>";

            return typeName;
        }

        void SetData(JProperty property)
        {
            if (property.First is not JObject p1 || p1.First is not JProperty p2)
                throw new ArgumentException("Argument expected to be {{get => }}");

            Type = p2.Name switch
            {
                "get" => ApiTypes.Get,
                "post" => ApiTypes.Post,
                "delete" => ApiTypes.Delete,
                _ => throw new ArgumentException("Invalid api type " + p2.Name)
            };

            if (p2.First == null)
                return;

            IsEndpoint = true;
            foreach(var param in p2.First.OfType<JProperty>())
            {
                if (param.First == null)
                    continue;

                switch(param.Name)
                {
                    case "responses":
                        var success = param.First.OfType<JProperty>().Where(x => x.Name == "200").FirstOrDefault();
                        if (success == null)
                            break;

                        var respDesc = success.Descendants().OfType<JProperty>().Where(x => x.Name == "schema").FirstOrDefault();
                        if (respDesc == null)
                            break;

                        ReturnType = DecodeTypeName(respDesc.Descendants());
                        break;

                    case "parameters":
                        var pms = JsonConvert.DeserializeObject<List<Parameter>>(param.First.ToString());
                        if (pms != null)
                            Parameters = pms;                     
                        break;

                    default: 
                        break;
                }
            }
        }

        private void Add(string[] path, JProperty data, string fullPath)
        {
            if(path.Length == 0)
            {
                SetData(data);
                return;
            }

            var name = path[0];
            var subPath = path.Skip(1).ToArray();
            foreach (var c in this)
            {
                if (c.Name == name)
                {
                    c.Add(subPath, data, fullPath);
                    return;
                }
            }

            var dt = new PathsTreeNode(name, fullPath);
            dt.Add(subPath, data, fullPath);
            this.Add(dt);
        }

        public void Add(string path, JProperty data) => Add(path.Split('/'), data, path);

        public PathsTreeNode? GetByPath(string[] paths)
        {
            if (paths.Length == 0)
                return this;

            foreach (var c in this)
                if (c.Name == paths[0])
                    return c.GetByPath(paths.Skip(1).ToArray());

            return null;
        }

        public PathsTreeNode? GetByPath(string path) => GetByPath(path.Split('/'));

        public string ToString(string overrideName)
        {
            if (!IsEndpoint)
                return Name;

            string header;
            var headerBuilder = new StringBuilder();
            headerBuilder.Append($"public static async Task<ApiResult<{ReturnType}>> {overrideName.ToTitleCase()}(");
            if (Parameters.Count != 0)
            {
                foreach (var p in Parameters)
                    headerBuilder.Append($"{p.Type} {p.Name}, ");

                headerBuilder.Remove(headerBuilder.Length - 2, 2);
            }
            headerBuilder.Append(")");
            header = headerBuilder.ToString();

            var contents = new StringBuilder();
            contents.Append($" => await Api.{Type}<{ReturnType}>(\"{Path}\"");
            foreach (var p in Parameters)
                contents.Append($", \"{p.Name}\".ToApiParam({p.Name})");
            contents.Append(");");

            return header + contents.ToString();
        }

        public override string ToString() => ToString(Name);

        public string Print(int depth = 0)
        {
            string ret = "";
            for (var i = 0; i < depth; i++)
                ret += "  ";

            ret += $"- {ToString()}\n";
            foreach (var c in this)
                ret += c.Print(depth + 1);

            return ret;
        }

        public PathsTreeNode(string name, string path)
        {
            this.Name = name;
            this.Path = path;
        }
    }
}
