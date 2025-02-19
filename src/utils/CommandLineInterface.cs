using System.Reflection;
using dnproto.commands;

namespace dnproto.utils;

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
        if (arguments.ContainsKey("debug") && GetArgumentValue(arguments, "debug") == "true")
        {
            Console.WriteLine("Waiting for debugger to attach.");

            while (!System.Diagnostics.Debugger.IsAttached)
            {
                System.Threading.Thread.Sleep(1000);
            }
        }


        //
        // Create command instance
        //
        BaseCommand? commandInstance = CommandLineInterface.TryCreateCommandInstance(commandName);

        if (commandInstance == null)
        {
            Console.WriteLine();
            Console.WriteLine($"Command not found: {commandName}");
            Console.WriteLine();

            commandInstance = CommandLineInterface.TryCreateCommandInstance("help");

            if(commandInstance == null)
            {
                throw new Exception("Help command not found.");
            }
            else
            {
                commandInstance.DoCommand(arguments);
                Console.WriteLine();
                return;
            }
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



    public static bool CheckArguments(dnproto.commands.BaseCommand command, Dictionary<string, string> userArguments)
    {
        var requiredArguments = command.GetRequiredArguments().Select(arg => arg.ToLower()).ToList();
        var optionalArguments = command.GetOptionalArguments().Select(arg => arg.ToLower()).ToList();
        var reservedArguments = GetReservedArguments();

        // Check for missing required arguments
        foreach (var requiredArgument in requiredArguments)
        {
            if (userArguments.ContainsKey(requiredArgument) == false)
            {
                return false;
            }
        }

        // Check for required arguments that clash with reserved arguments
        foreach (var requiredArgument in requiredArguments)
        {
            if (reservedArguments.Contains(requiredArgument))
            {
                return false;
            }
        }

        // Check for unknown arguments
        foreach (var userArgument in userArguments)
        {
            if (requiredArguments.Contains(userArgument.Key) == false 
                && optionalArguments.Contains(userArgument.Key) == false 
                && reservedArguments.Contains(userArgument.Key) == false)
            {
                return false;
            }
        }

        // Check for optional arguments that clash with reserved arguments
        foreach (var optionalArgument in optionalArguments)
        {
            if (reservedArguments.Contains(optionalArgument))
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

    public static HashSet<string> GetReservedArguments()
    {
        return new HashSet<string>(new string[] { "command", "debug" });
    }

    public static string? GetArgumentValue(Dictionary<string, string> arguments, string argumentName)
    {
        if(arguments.ContainsKey(argumentName.ToLower()) == false)
        {
            return null;
        }

        return arguments[argumentName.ToLower()];
    }

    public static int GetArgumentValueWithDefault(Dictionary<string, string> arguments, string argumentName, int defaultValue)
    {
        if(arguments.ContainsKey(argumentName.ToLower()) == false)
        {
            return defaultValue;
        }

        int val = 0;

        if(int.TryParse(arguments[argumentName.ToLower()], out val))
        {
            return val;
        }

        return defaultValue;
    }

    public static bool GetArgumentValueWithDefault(Dictionary<string, string> arguments, string argumentName, bool defaultValue)
    {
        if(arguments.ContainsKey(argumentName.ToLower()) == false)
        {
            return defaultValue;
        }

        bool val;

        if(bool.TryParse(arguments[argumentName.ToLower()], out val))
        {
            return val;
        }

        return defaultValue;
    }

    public static string GetArgumentValueWithDefault(Dictionary<string, string> arguments, string argumentName, string defaultValue)
    {
        return arguments.ContainsKey(argumentName.ToLower()) ? arguments[argumentName.ToLower()] : defaultValue;
    }


    public static bool HasArgument(Dictionary<string, string> arguments, string argumentName)
    {
        return arguments != null && arguments.ContainsKey(argumentName.ToLower());
    }

    public static void PrintLineSeparator()
    {
        Console.WriteLine();
        Console.WriteLine("---------------------------------------------------------");
        Console.WriteLine();
    }


}