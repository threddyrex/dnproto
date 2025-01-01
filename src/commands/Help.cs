namespace dnproto.commands
{
    public class Help : ICommand
    {
        public HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>();
        }

        public HashSet<string> GetOptionalArguments()
        {
            return new HashSet<string>();
        }
        
        public void DoCommand(Dictionary<string, string> arguments)
        {
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine();
            Console.WriteLine("    dnproto /command commandname [/arg1 val1 /arg2 val2]");
            Console.WriteLine();
            Console.WriteLine("Available commands:");
            Console.WriteLine();
            var commands = CommandHelpers.GetAllCommandTypes();
            foreach (var command in commands.OrderBy(c => c.Name))
            {
                Console.WriteLine("    " + command.Name);
            }
            Console.WriteLine();
        }
    }
}