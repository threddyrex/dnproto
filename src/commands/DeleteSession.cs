using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.utils;

namespace dnproto.commands
{
    public class DeleteSession : BaseCommand
    {
        /// <summary>
        /// Delete session
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="ArgumentException"></exception>
        public override void DoCommand(Dictionary<string, string> arguments)
        {
            Console.WriteLine("Deleting session.");
            LocalStateSession.WriteSessionProperties(new Dictionary<string, string>
            {
                {"did", ""},
                {"pds", ""},
                {"accessJwt", ""},
                {"refreshJwt", ""}
            });
        }
    }
}