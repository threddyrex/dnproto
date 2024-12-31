namespace dnproto.commands
{
    public class List : ICommand
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
            Console.WriteLine("Available commands:");
            var commands = CommandHelpers.GetAllCommandTypes();
            foreach (var command in commands)
            {
                Console.WriteLine("    " + command.Name);
            }
        }
    }
}