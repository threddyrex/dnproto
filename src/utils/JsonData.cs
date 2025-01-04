using System.Text.Json;
using System.Text.Json.Nodes;

namespace dnproto.utils
{   
    public static class JsonData
    {
        public static string GetPropertyValue (JsonNode? node, string propertyName)
        {
            if (node == null)
            {
                return "";
            }

            var objValue = node[propertyName];

            if(objValue != null)
            {
                return objValue.ToString();
            }

            return "";
        }

        public static void WriteJsonToFile(JsonNode? node, string outputFilePath)
        {
            if(node == null || string.IsNullOrEmpty(outputFilePath))
            {
                return;
            }

            Console.WriteLine($"Writing JSON to file: {outputFilePath}");

            var options = new JsonSerializerOptions { WriteIndented = true };
            System.IO.File.WriteAllText(outputFilePath, node.ToJsonString(options));
        }


        public static JsonNode? ReadJsonFromFile(string inputFilePath)
        {
            if(string.IsNullOrEmpty(inputFilePath))
            {
                return null;
            }

            if(!System.IO.File.Exists(inputFilePath))
            {
                return null;
            }

            var jsonString = System.IO.File.ReadAllText(inputFilePath);
            return JsonNode.Parse(jsonString);
        }
    }
}