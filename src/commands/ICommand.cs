namespace dnproto.commands
{
    public interface ICommand
    {
        HashSet<string> GetRequiredArguments();

        HashSet<string> GetOptionalArguments();

        void DoCommand(Dictionary<string, string> arguments);

    }
}