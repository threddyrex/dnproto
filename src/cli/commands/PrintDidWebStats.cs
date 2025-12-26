using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using dnproto.fs;
using dnproto.repo;
using dnproto.ws;
using dnproto.uri;

namespace dnproto.cli.commands;

public class PrintDidWebStats : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[] { "stateJsonFile" });
    }



    /// <summary>
    /// Print stats for the didwebs listed in the state file.
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get arguments
        //
        string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
        string? stateJsonFile = CommandLineInterface.GetArgumentValue(arguments, "stateJsonFile");

        Logger.LogTrace($"dataDir: {dataDir}");
        Logger.LogTrace($"stateJsonFile: {stateJsonFile}");
        Logger.LogTrace($"state file exists: {File.Exists(stateJsonFile)}");


        //
        // Load lfs
        //
        LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);

        //
        // Load state
        //
        JsonObject? stateNode = JsonData.ReadJsonObjectFromFile(stateJsonFile);
        JsonObject? didWebs = stateNode?["firehose"]?["didWebs"]?.AsObject();

        if (didWebs == null)
        {
            Logger.LogError("Null didwebs. Returning.");
            return;
        }

        //
        // Variables for stats.
        //
        int totalDidWebs = 0;
        int totalRepos = 0;
        int totalDateFound = 0;
        int totalThisMonth = 0;
        int totalThisWeek = 0;

        Dictionary<string, string> didWebLatestDates = new Dictionary<string, string>();


        //
        // Loop through the didwebs
        //
        foreach (KeyValuePair<string, JsonNode?> pair in didWebs)
        {
            totalDidWebs++;

            string? didWeb = pair.Key;
            string? pds = pair.Value?["pds"]?.ToString()?.Replace("https://", "")?.Replace("/", "");
            string? repoFile = lfs?.GetPath_RepoFile(didWeb);

            Logger.LogInfo($"didweb: '{didWeb}'  pds: '{pds}'  repoFile: '{repoFile}'");

            if (File.Exists(repoFile))
            {
                totalRepos++;

                try
                {
                    bool foundDate = false;
                    DateTime latestDate = DateTime.MinValue;

                    // walk repo (full parse)
                    Repo.WalkRepo(
                        repoFile,
                        (repoHeader) =>
                        {
                            return true;
                        },
                        (repoRecord) =>
                        {
                            if (string.IsNullOrEmpty(repoRecord.RecordType)) return true;


                            if (DateTime.TryParse(repoRecord.CreatedAt, out DateTime createdAt))
                            {
                                foundDate = true;
                                if (createdAt > latestDate)
                                {
                                    latestDate = createdAt;
                                }
                            }
                            return true;
                        }
                    );

                    if (foundDate)
                    {
                        totalDateFound++;

                        if (latestDate > DateTime.Now.AddMonths(-1))
                        {
                            totalThisMonth++;
                        }
                        if (latestDate > DateTime.Now.AddDays(-7))
                        {
                            totalThisWeek++;
                        }

                        didWebLatestDates[didWeb] = latestDate.ToString();
                    }
                    else
                    {
                        didWebLatestDates[didWeb] = "";
                    }

                    Logger.LogInfo($"foundDate: {foundDate}  latestDate: {latestDate}");
                }
                catch (Exception ex)
                {
                    Exception? inner = ex;
                    int count = 1;
                    while (inner != null)
                    {
                        Logger.LogError($"Exception {count}: {inner.Message}");
                        Logger.LogTrace(inner.StackTrace ?? "");
                        inner = inner.InnerException;
                        count++;
                    }

                }
            }
        }


        Logger.LogInfo($"totalDidWebs: {totalDidWebs}");
        Logger.LogInfo($"totalRepos: {totalRepos}");
        Logger.LogInfo($"totalDateFound: {totalDateFound}");
        Logger.LogInfo($"totalThisMonth: {totalThisMonth}");
        Logger.LogInfo($"totalThisWeek: {totalThisWeek}");


        foreach(string key in didWebLatestDates.Keys)
        {
            Console.WriteLine($"{key},{didWebLatestDates[key]}");
        }

    }
}