

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using dnproto.log;

namespace dnproto.pds
{
    /// <summary>
    /// A minimal ASP.NET Core web API that responds to /hello with "world"
    /// </summary>
    public class Pds
    {
        private readonly BaseLogger Logger;
        private readonly string port;

        public Pds(BaseLogger logger, string port)
        {
            Logger = logger;
            this.port = port;
        }


        public void Run()
        {
            Logger.LogInfo($"Starting minimal API with HTTPS on port {port}...");

            var builder = WebApplication.CreateBuilder();
            
            // Configure to listen on specified port with HTTPS
            builder.WebHost.UseUrls($"https://localhost:{port}");
            
            var app = builder.Build();

            // Enable HTTPS redirection
            app.UseHttpsRedirection();

            // Define the /hello endpoint
            app.MapGet("/hello", () => "world");

            Logger.LogInfo($"Server running at https://localhost:{port}/hello");
            
            app.Run();
        }
    }
}
