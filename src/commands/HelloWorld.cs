namespace dnproto.commands
{
    public class HelloWorld : BaseCommand
    {
        public override void DoCommand(Dictionary<string, string> arguments)
        {
            Console.WriteLine("Hello, World! This is a test command.");
        }
    }
}