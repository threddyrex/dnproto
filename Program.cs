



// Parse args
var arguments = dnproto.commands.CommandHelpers.ParseArguments(args);
Console.WriteLine("Parsed arguments:");
foreach (var kvp in arguments)
{
    Console.WriteLine($"    {kvp.Key}: {kvp.Value}");
}


// Run
dnproto.commands.CommandHelpers.Run(arguments);




