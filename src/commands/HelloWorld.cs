namespace dnproto.commands
{
    public class HelloWorld : ICommand
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
            Console.WriteLine("Hello, World! This is a test command.");
        }


    }
}