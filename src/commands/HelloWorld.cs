namespace dnproto.commands
{
    /// <summary>
    /// A test command that prints "Hello, World!" to the console.
    /// </summary>
    public class HelloWorld : BaseCommand
    {
        public override void DoCommand(Dictionary<string, string> arguments)
        {
            Console.WriteLine("Hello, World! This is a test command.");
        }
    }
}