using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace dnproto.repo;

/// <summary>
/// Just some helpers for working with json.
/// </summary>
public static class JsonData
{
    /// <summary>
    /// Find node at path and return it. Kinda like xpath.
    /// </summary>
    /// <param name="node"></param>
    /// <param name="propertyNames"></param>
    /// <returns></returns>
    public static JsonNode? SelectNode (JsonNode? node, string[] propertyNames)
    {
        JsonNode? current = node;

        foreach(var propertyName in propertyNames)
        {
            if(current == null) return null;
            current = current[propertyName];
        }

        return current;
    }

    /// <summary>
    /// Find node at a path, and return string value for it.
    /// </summary>
    /// <param name="node"></param>
    /// <param name="propertyNames"></param>
    /// <returns></returns>
    public static string? SelectString(JsonNode? node, string[] propertyNames)
    {
        var selectedNode = SelectNode(node, propertyNames);
        return selectedNode != null ? selectedNode.ToString() : null;
    }

    public static string? SelectString(JsonNode? node, string propertyName)
    {
        return SelectString(node, [propertyName]);
    }

    public static List<string> SelectStringArray(JsonNode? node, string propertyName)
    {
        var selectedNode = SelectNode(node, [propertyName]);
        var result = new List<string>();
        if(selectedNode == null) return result;
        if(selectedNode is JsonArray jsonArray)
        {
            foreach(var item in jsonArray)
            {
                if(item != null)
                {
                    result.Add(item.ToString());
                }
            }
        }
        return result;
    }



    /// <summary>
    /// Convert json node to json string.
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public static string? ConvertToJsonString(JsonNode? node)
    {
        if(node == null) return null;
        var options = new JsonSerializerOptions { WriteIndented = true };
        return node.ToJsonString(options);
    }


    /// <summary>
    /// Convert object to json string.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    [RequiresUnreferencedCode("PublishTrimmmed is false")]
    [RequiresDynamicCode("PublishTrimmmed is false")]
    public static string? ConvertObjectToJsonString(object? obj)
    {
        if (obj == null) return null;
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(obj, options);
    }

    [RequiresUnreferencedCode("PublishTrimmmed is false")]
    [RequiresDynamicCode("PublishTrimmmed is false")]
    public static object? ConvertJsonStringToObject(string? jsonString)
    {
        if(string.IsNullOrEmpty(jsonString)) return null;
        return JsonSerializer.Deserialize<object>(jsonString);
    }


    /// <summary>
    /// Write json node to file.
    /// </summary>
    /// <param name="node"></param>
    /// <param name="outputFilePath"></param>
    public static void WriteJsonToFile(JsonNode? node, string? outputFilePath)
    {
        if(node == null || string.IsNullOrEmpty(outputFilePath)) return;
        string? jsonString = ConvertToJsonString(node);
        WriteJsonToFile(jsonString, outputFilePath);
    }


    public static void WriteJsonToFile(string? jsonString, string? outputFilePath)
    {
        if(string.IsNullOrEmpty(jsonString) || string.IsNullOrEmpty(outputFilePath)) return;
        System.IO.File.WriteAllText(outputFilePath, jsonString);
    }


    /// <summary>
    /// Reads json node from file.
    /// </summary>
    /// <param name="inputFilePath"></param>
    /// <returns></returns>
    public static JsonNode? ReadJsonFromFile(string? inputFilePath)
    {
        if (string.IsNullOrEmpty(inputFilePath)) return null;
        if (!System.IO.File.Exists(inputFilePath)) return null;
        var jsonString = System.IO.File.ReadAllText(inputFilePath);
        return JsonNode.Parse(jsonString);
    }
    
    /// <summary>
    /// Reads json object from file.
    /// </summary>
    /// <param name="inputFilePath"></param>
    /// <returns></returns>
    public static JsonObject? ReadJsonObjectFromFile(string? inputFilePath)
    {
        if(string.IsNullOrEmpty(inputFilePath)) return null;
        if(!System.IO.File.Exists(inputFilePath)) return null;
        var jsonString = System.IO.File.ReadAllText(inputFilePath);
        return JsonObject.Parse(jsonString)?.AsObject();
    }
}