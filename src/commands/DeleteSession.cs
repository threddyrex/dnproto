using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.helpers;

namespace dnproto.commands
{
    public class DeleteSession : ICommand
    {
        public HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>();
        }

        public HashSet<string> GetOptionalArguments()
        {
            return new HashSet<string>();
        }


        /// <summary>
        /// Delete session
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="ArgumentException"></exception>
        public void DoCommand(Dictionary<string, string> arguments)
        {
            DoDeleteSession();
        }

        
        /// <summary>
        /// Delete session
        /// </summary>
        /// <returns></returns>
        public static void DoDeleteSession()
        {

            string sessionDid = "";
            string sessionPds = "";
            string sessionAccessJwt = "";
            string sessionRefreshJwt = "";

            LocalStateHelpers.WriteSessionProperties(new Dictionary<string, string>
            {
                {"sessionDid", sessionDid},
                {"sessionPds", sessionPds},
                {"sessionAccessJwt", sessionAccessJwt},
                {"sessionRefreshJwt", sessionRefreshJwt}
            });
        }
    }
}