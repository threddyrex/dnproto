using System.Text;

using dnproto.sdk.repo;
using dnproto.sdk.ws;
using dnproto.sdk.fs;
using dnproto.sdk.mst;

namespace dnproto.cli.commands
{
    public class PrintRepoMstInfo : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"actor"});
        }

        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get arguments
            //
            string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
            string? actor = CommandLineInterface.GetArgumentValue(arguments, "actor");

            //
            // Load lfs
            //
            LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);
            ActorInfo? actorInfo = lfs?.ResolveActorInfo(actor);

            //
            // Get local repo file
            //
            string? repoFile = LocalFileSystem.Initialize(dataDir, Logger)?.GetPath_RepoFile(actorInfo);
            if (string.IsNullOrEmpty(repoFile) || File.Exists(repoFile) == false)
            {
                Logger.LogError($"Repo file does not exist: {repoFile}");
                return;
            }


            //
            // Load mst
            //
            MstRepository? mstRepo = MstRepository.LoadFromFile(repoFile, Logger);

            if( mstRepo == null || mstRepo.CurrentCommit == null)
            {
                Logger.LogError($"Failed to load MST repository from file: {repoFile}");
                return;
            }

            Logger.LogInfo($"did: {mstRepo.Did}");
            Logger.LogInfo($"sequence number: {mstRepo.SequenceNumber}");
            Logger.LogInfo($"number of MST nodes: {mstRepo.Mst.GetAllNodes().Count}");
            Logger.LogInfo($"root entries count: {mstRepo.Mst.Root?.Entries.Count ?? 0}");

            // Calculate and display maximum depth
            if (mstRepo.Mst.Root != null)
            {
                int maxDepth = CalculateMaxDepth(mstRepo, mstRepo.Mst.Root, 0);
                Logger.LogInfo($"maximum tree depth: {maxDepth}");
            }
            else
            {
                Logger.LogInfo($"maximum tree depth: 0");
            }

            // traverse tree and print it out, with each level indented by one space
            if (mstRepo.Mst.Root != null)
            {
                Logger.LogInfo("MST Tree Structure:");
                var recordCache = mstRepo.Mst.GetAllRecords();
                PrintMstNode(mstRepo, mstRepo.Mst.Root, "", Array.Empty<byte>(), recordCache);
            }
            else
            {
                Logger.LogInfo("MST Root is null - empty repository");
            }

        }

        int CalculateMaxDepth(MstRepository mstRepo, MstNode node, int currentDepth)
        {
            int maxDepth = currentDepth;

            // Check left child
            if (node.LeftCid != null)
            {
                MstNode? leftChild = mstRepo.Mst.GetNodeByCid(node.LeftCid.Base32);
                if (leftChild != null)
                {
                    maxDepth = Math.Max(maxDepth, CalculateMaxDepth(mstRepo, leftChild, currentDepth + 1));
                }
            }

            // Check tree children in entries
            foreach (var entry in node.Entries)
            {
                if (entry.TreeCid != null)
                {
                    MstNode? childNode = mstRepo.Mst.GetNodeByCid(entry.TreeCid.Base32);
                    if (childNode != null)
                    {
                        maxDepth = Math.Max(maxDepth, CalculateMaxDepth(mstRepo, childNode, currentDepth + 1));
                    }
                }
            }

            return maxDepth;
        }

        void PrintMstNode(MstRepository mstRepo, MstNode node, string prefix, byte[] currentPrefix, Dictionary<string, byte[]> recordCache)
        {
            // Handle left child (keys before all entries in this node)
            if (node.LeftCid != null)
            {
                MstNode? leftChild = mstRepo.Mst.GetNodeByCid(node.LeftCid.Base32);
                if (leftChild != null)
                {
                    PrintMstNode(mstRepo, leftChild, prefix + "  ", currentPrefix, recordCache);
                }
            }

            byte[] prevKey = currentPrefix;
            
            foreach (var entry in node.Entries)
            {
                // Build prefix from previous key
                byte[] entryPrefix = prevKey.Take(entry.PrefixLength).ToArray();
                byte[] fullKey = entry.GetFullKey(entryPrefix);
                string keyStr = Encoding.UTF8.GetString(fullKey);
                
                // Try to get record data and extract createdAt for posts
                string recordInfo = "";
                if (keyStr.Contains("app.bsky.feed.post"))
                {
                    if (recordCache.TryGetValue(entry.ValueCid.Base32, out var recordData))
                    {
                        //Logger.LogTrace($"Found record data for {keyStr}, size: {recordData.Length}");
                        try
                        {
                            var recordObj = DagCborObject.ReadFromStream(new MemoryStream(recordData));
                            //Logger.LogTrace($"Parsed record obj, type: {recordObj.Value?.GetType().Name}");
                            
                            if (recordObj.Value is Dictionary<string, DagCborObject> recordDict)
                            {
                                //Logger.LogTrace($"Record has {recordDict.Count} fields: {string.Join(", ", recordDict.Keys)}");
                                
                                if (recordDict.TryGetValue("createdAt", out var createdAtObj))
                                {
                                    //Logger.LogTrace($"createdAt found, type: {createdAtObj.Value?.GetType().Name}, value: {createdAtObj.Value}");
                                    recordInfo = $", createdAt: {createdAtObj.Value}";
                                }
                                else
                                {
                                    //Logger.LogTrace("createdAt field not found in record");
                                }
                            }
                        }
                        catch (Exception)
                        {
                            //Logger.LogTrace($"Error parsing record: {ex.Message}");
                        }
                    }
                    else
                    {
                        //Logger.LogTrace($"Record not found in cache for CID: {entry.ValueCid.Base32}");
                    }
                }
                
                Logger.LogTrace($"{prefix}- key: {keyStr}, valueCid: {entry.ValueCid.Base32}, treeCid: {entry.TreeCid?.Base32}{recordInfo}");

                if (entry.TreeCid != null)
                {
                    MstNode? childNode = mstRepo.Mst.GetNodeByCid(entry.TreeCid.Base32);
                    if (childNode != null)
                    {
                        PrintMstNode(mstRepo, childNode, prefix + "  ", fullKey, recordCache);
                    }
                }
                
                // Update prevKey for next entry
                prevKey = fullKey;
            }
        }

   }
}