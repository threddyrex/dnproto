namespace dnproto.commands
{
    public class HelloWorld : ICommand
    {
        public void DoCommand(Dictionary<string, string> arguments)
        {
            Console.WriteLine("Hello, World! This is a test command.");
        }
    }
}