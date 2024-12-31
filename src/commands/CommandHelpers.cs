using System.Reflection;

namespace dnproto.commands
{
    public static class CommandHelpers
    {
        /// <summary>
        /// Runs console program.
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="Exception"></exception>
        public static void Run(string[] args)
        {
            // Parse args
            PrintLineSeparator();
            var arguments = dnproto.commands.CommandHelpers.ParseArguments(args);
            Console.WriteLine("Parsed arguments:");
            foreach (var kvp in arguments)
            {
                Console.WriteLine($"    {kvp.Key}: {kvp.Value}");
            }

            if (arguments.ContainsKey("command") == false)
            {
                throw new Exception("Missing required argument: command");
            }

            string commandName = arguments["command"];


            // Create command instance
            ICommand? commandInstance = CommandHelpers.TryCreateCommandInstance(commandName);

            if (commandInstance == null)
            {
                throw new Exception($"Command '{arguments["command"]}' not found.");
            }


            // Assert arguments. This will throw if the arguments specified by the command aren't matched.
            CommandHelpers.AssertArguments(commandInstance, arguments);

 
            // Do command
            Console.WriteLine($"Running command...");
            PrintLineSeparator();
            commandInstance.DoCommand(arguments);
            PrintLineSeparator();

        }


        /// <summary>
        /// Parses command line arguments into a dictionary.
        /// </summary>
        /// <param name="args">Array of command line arguments.</param>
        /// <returns>A dictionary where the key is the argument name and the value is the argument value.</returns>
        public static Dictionary<string, string> ParseArguments(string[] args)
        {
            if(args == null)
            {
                throw new Exception("Arguments cannot be null.");
            }
            
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
                        arguments[args[i].Substring(1).ToLower()] = args[i + 1];
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
        /// Tries to create an instance of a command by its name.
        /// </summary>
        /// <param name="commandName"></param>
        /// <returns></returns>
        public static ICommand? TryCreateCommandInstance(string commandName)
        {
            var commandType = TryFindCommandType(commandName);
            return (commandType is not null ? Activator.CreateInstance(commandType) as ICommand : null);
        }

        /// <summary>
        /// Finds a class in the given assembly by its namespace and case-insensitive name.
        /// </summary>
        /// <param name="assembly">The assembly to search.</param>
        /// <param name="className">The case-insensitive name of the class to find.</param>
        /// <returns>The Type of the class if found, otherwise null.</returns>
        public static Type? TryFindCommandType(string className)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            foreach (Type type in assembly.GetTypes())
            {
                if (type.Namespace == "dnproto.commands" && string.Equals(type.Name, className, StringComparison.OrdinalIgnoreCase))
                {
                    return type;
                }
            }

            return null;
        }

        public static List<Type> GetAllCommandTypes()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            List<Type> commands = new List<Type>();

            foreach (Type type in assembly.GetTypes())
            {
                if (type.Namespace == "dnproto.commands" && typeof(ICommand).IsAssignableFrom(type) && type.Name != "ICommand")
                {
                    commands.Add(type);
                }
            }

            return commands;
        }



        public static void AssertArguments(ICommand command, Dictionary<string, string> arguments)
        {
            var requiredArguments = command.GetRequiredArguments();
            var optionalArguments = command.GetOptionalArguments();

            // Check for missing required arguments
            foreach (var requiredArgument in requiredArguments)
            {
                if (arguments.ContainsKey(requiredArgument) == false)
                {
                    throw new ArgumentException($"Missing required argument: {requiredArgument}");
                }
            }

            // Check for unknown arguments
            foreach (var argument in arguments)
            {
                if (requiredArguments.Contains(argument.Key) == false && optionalArguments.Contains(argument.Key) == false && argument.Key != "command")
                {
                    throw new ArgumentException($"Unknown argument: {argument.Key}");
                }
            }
        }


        public static void PrintLineSeparator()
        {
            Console.WriteLine("---------------------------------------------------------");
        }


    }
}