using System.Text.Json;
using System.Text.Json.Nodes;

namespace dnproto.utils
{   
    public static class JsonReader
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
    }
}