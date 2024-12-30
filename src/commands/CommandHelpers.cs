using System.Reflection;

namespace dnproto.commands
{
    public static class CommandHelpers
    {
        /// <summary>
        /// Parses command line arguments into a dictionary.
        /// </summary>
        /// <param name="args">Array of command line arguments.</param>
        /// <returns>A dictionary where the key is the argument name and the value is the argument value.</returns>
        public static Dictionary<string, string> ParseArguments(string[] args)
        {
            string formatError = "Command line arguments must be in the format '/name1 value1 /name2 value2'";

            // Check that there are an even number of arguments
            if (args.Length % 2 != 0)
            {
                throw new Exception(formatError);
            }

            // Loop and turn them into key/value pairs
            var arguments = new Dictionary<string, string>();
            for (int i = 0; i < args.Length; i += 2)
            {
                if (i + 1 < args.Length)
                {
                    if (args[i].StartsWith("/"))
                    {
                        arguments[args[i].Substring(1)] = args[i + 1];
                    }
                    else
                    {
                        throw new Exception(formatError);
                    }
                }
                else
                {
                    throw new Exception(formatError);
                }
            }
            
            return arguments;
        }

        /// <summary>
        /// Finds a class in the given assembly by its namespace and case-insensitive name.
        /// </summary>
        /// <param name="assembly">The assembly to search.</param>
        /// <param name="className">The case-insensitive name of the class to find.</param>
        /// <returns>The Type of the class if found, otherwise null.</returns>
        public static Type FindCommandType(string className)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            foreach (Type type in assembly.GetTypes())
            {
                if (type.Namespace == "dnproto.commands" && string.Equals(type.Name, className, StringComparison.OrdinalIgnoreCase))
                {
                    return type;
                }
            }

            throw new Exception("Type not found for command: " + className);
        }


        /// <summary>
        /// Runs console program.
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="Exception"></exception>
        public static void Run(Dictionary<string, string> arguments)
        {
            // Do command
            if (arguments.TryGetValue("command", out var commandName))
            {
                var commandType = dnproto.commands.CommandHelpers.FindCommandType(commandName);

                if (commandType != null)
                {
                    var commandInstance = Activator.CreateInstance(commandType) as dnproto.commands.ICommand;
                    if (commandInstance != null)
                    {
                        Console.WriteLine($"Running command: " + commandType.Namespace + " " + commandType.Name);
                        commandInstance.DoCommand(arguments);
                    }
                    else
                    {
                        throw new Exception($"Class '{commandName}' does not implement 'dnproto.commands.ICommand'.");
                    }
                }
                else
                {
                    throw new Exception($"Class '{commandName}' not found in namespace 'dnproto.commands'.");
                }
            }
            else
            {
                throw new Exception("Argument 'command' not found.");
            }
        }
    }
}