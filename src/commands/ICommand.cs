namespace dnproto.commands
{
    public interface ICommand
    {
        void DoCommand(Dictionary<string, string> arguments);
    }
}