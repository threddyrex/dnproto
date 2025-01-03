namespace dnproto.commands
{
    public abstract class BaseCommand
    {
        public virtual HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>();
        }

        public virtual HashSet<string> GetOptionalArguments()
        {
            return new HashSet<string>();
        }

        public abstract void DoCommand(Dictionary<string, string> arguments);

    }
}