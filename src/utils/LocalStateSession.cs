using System.Text.Json;
using System.Text.Json.Nodes;

namespace dnproto.utils
{
    /// <summary>
    /// This class contains helper methods for managing local state directories 
    /// and session files for the dnproto application.
    ///
    /// For example: "login as user, then do some stuff..."
    /// 
    /// </summary>
    public static class LocalStateSession
    {
        public static string GetLocalStateDirectory()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dnproto");
        }

        public static string GetLocalStateSessionFile()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dnproto", "session.json");
        }

        public static void EnsureLocalStateDirectory()
        {
            string dir = GetLocalStateDirectory();
            Console.WriteLine($"Local state directory:  {dir}");
            if(Directory.Exists(dir) == false)
            {
                Console.WriteLine($"Local state directory doesn't exist. Creating.");
                Directory.CreateDirectory(dir);
            }
        }

        public static void EnsureLocalStateSessionFile()
        {
            string filePath = GetLocalStateSessionFile();
            Console.WriteLine($"Local session file:     {filePath}");
            if(File.Exists(filePath) == false)
            {
                Console.WriteLine($"Local session file doesn't exist. Creating.");
                File.WriteAllText(filePath, "{}");
            }
        }

        public static void WriteSessionProperty(string key, string value)
        {
            string filePath = GetLocalStateSessionFile();
            var json = File.ReadAllText(filePath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            if(dict != null)
            {
                dict[key] = value;
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(filePath, JsonSerializer.Serialize(dict, options));
            }
            else
            {
                throw new Exception("Failed to deserialize session file.");
            }
        }

        public static void WriteSessionProperties(Dictionary<string, string> properties)
        {
            string filePath = GetLocalStateSessionFile();
            var json = File.ReadAllText(filePath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            if(dict != null)
            {
                foreach(string key in properties.Keys)
                {
                    dict[key] = properties[key];
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(filePath, JsonSerializer.Serialize(dict, options));
            }
            else
            {
                throw new Exception("Failed to deserialize session file.");
            }
        }


        public static string ReadSessionProperty(string key)
        {
            string filePath = GetLocalStateSessionFile();
            var json = File.ReadAllText(filePath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            if(dict != null)
            {
                if(dict.ContainsKey(key))
                {
                    return dict[key];
                }
                else
                {
                    return "";
                }
            }
            else
            {
                throw new Exception("Failed to deserialize session file.");
            }
        }
    }
}