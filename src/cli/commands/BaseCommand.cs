
using dnproto.log;

namespace dnproto.cli.commands
{
    /// <summary>
    /// The user runs the tool and specifies one command to run. All of the commands inherit from this class.
    /// </summary>
    public abstract class BaseCommand
    {
        public BaseLogger Logger = new NullLogger();

        /// <summary>
        /// These arguments *must* be specified by the user when running the command.
        /// </summary>
        /// <returns></returns>
        public virtual HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>();
        }

        /// <summary>
        /// These arguments *can* be specified by the user when running the command.
        /// </summary>
        /// <returns></returns>
        public virtual HashSet<string> GetOptionalArguments()
        {
            return new HashSet<string>();
        }

        /// <summary>
        /// Run the command.
        /// </summary>
        /// <param name="arguments"></param>
        public abstract void DoCommand(Dictionary<string, string> arguments);

    }
}