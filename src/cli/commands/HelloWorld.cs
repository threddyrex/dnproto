namespace dnproto.cli.commands
{
    /// <summary>
    /// A test command that prints "Hello, World!" to the console.
    /// </summary>
    public class HelloWorld : BaseCommand
    {
        public override void DoCommand(Dictionary<string, string> arguments)
        {
            Logger.LogInfo("Hello, World! This is a test command.");
        }
    }
}