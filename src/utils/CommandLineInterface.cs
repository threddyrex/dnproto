using System.Reflection;
using dnproto.commands;

namespace dnproto.utils
{
    public static class CommandLineInterface
    {
        /// <summary>
        /// Runs console program.
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="Exception"></exception>
        public static void RunMain(string[] args)
        {
            //
            // Parse args
            //
            PrintLineSeparator();
            var arguments = ParseArguments(args);
            Console.WriteLine("Parsed arguments length: " + arguments.Keys.Count);
            if(arguments.Keys.Count > 0)
            {
                Console.WriteLine("Parsed arguments:");
                foreach (var kvp in arguments)
                {
                    if(kvp.Key == "password")
                    {
                        Console.WriteLine($"    {kvp.Key}: ********");
                    }
                    else
                    {
                        Console.WriteLine($"    {kvp.Key}: {kvp.Value}");
                    }
                }
            }

            if (arguments.ContainsKey("command") == false)
            {
                arguments["command"] = "Help";
            }

            string commandName = arguments["command"];


            //
            // Do we want to debug?
            //
            if (arguments.ContainsKey("debugattach"))
            {
                Console.WriteLine("Waiting for debugger to attach. Press any key to continue.");
                Console.ReadKey();
            }

            //
            // Print local state directory
            //
            LocalStateSession.EnsureLocalStateDirectory();
            LocalStateSession.EnsureLocalStateSessionFile();
            Console.WriteLine("Last command run: " + LocalStateSession.ReadSessionProperty("lastCommand"));
            Console.WriteLine("Last command run time: " + LocalStateSession.ReadSessionProperty("lastRunTime"));
            LocalStateSession.WriteSessionProperty("lastCommand", commandName);
            LocalStateSession.WriteSessionProperty("lastRunTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));


            //
            // Create command instance
            //
            BaseCommand? commandInstance = CommandLineInterface.TryCreateCommandInstance(commandName);

            if (commandInstance == null)
            {
                throw new Exception($"Command '{arguments["command"]}' not found.");
            }


            //
            // Check that arguments exist. If not, print arguments and return.
            //
            if(CommandLineInterface.CheckArguments(commandInstance, arguments) == false)
            {
                PrintLineSeparator();
                Console.WriteLine("");
                CommandLineInterface.PrintArguments(commandName, commandInstance);
                PrintLineSeparator();
                return;
            }

 
            // Do command
            Console.WriteLine($"Running command.");
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

        public static string GetArgumentValue(Dictionary<string, string> arguments, string argumentName)
        {
            if(arguments.ContainsKey(argumentName) == false)
            {
                return "";
            }

            return arguments[argumentName];
        }



        /// <summary>
        /// Tries to create an instance of a command by its name.
        /// </summary>
        /// <param name="commandName"></param>
        /// <returns></returns>
        public static dnproto.commands.BaseCommand? TryCreateCommandInstance(string commandName)
        {
            var commandType = TryFindCommandType(commandName);
            return (commandType is not null ? Activator.CreateInstance(commandType) as dnproto.commands.BaseCommand : null);
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
                if (type.Namespace == "dnproto.commands" && typeof(BaseCommand).IsAssignableFrom(type) && type.Name != "BaseCommand")
                {
                    commands.Add(type);
                }
            }

            return commands;
        }



        public static bool CheckArguments(dnproto.commands.BaseCommand command, Dictionary<string, string> arguments)
        {
            var requiredArguments = command.GetRequiredArguments();
            var optionalArguments = command.GetOptionalArguments();

            // Check for missing required arguments
            foreach (var requiredArgument in requiredArguments)
            {
                if (arguments.ContainsKey(requiredArgument) == false)
                {
                    return false;
                }
            }

            // Check for unknown arguments
            foreach (var argument in arguments)
            {
                if (requiredArguments.Contains(argument.Key) == false && optionalArguments.Contains(argument.Key) == false && argument.Key != "command" && argument.Key != "debugattach")
                {
                    return false;
                }
            }

            return true;
        }

        public static void PrintArguments(string commandName, dnproto.commands.BaseCommand commandInstance)
        {
            Console.WriteLine("Usage:");
            string usage = "    .\\dnproto.exe /command " + commandName + "";

            // Required arguments
            foreach (var requiredArgument in commandInstance.GetRequiredArguments())
            {
                usage += " /" + requiredArgument + " val ";
            }

            // Optional arguments
            foreach (var optionalArgument in commandInstance.GetOptionalArguments())
            {
                usage += " [/" + optionalArgument + " val] ";
            }

            Console.WriteLine();
            Console.WriteLine(usage);
            Console.WriteLine();
        }


        public static void PrintLineSeparator()
        {
            Console.WriteLine();
            Console.WriteLine("---------------------------------------------------------");
            Console.WriteLine();
        }


    }
}