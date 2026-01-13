

using System.Globalization;
using dnproto.fs;
using dnproto.log;

namespace dnproto.pds;


public class BackgroundJobs
{
    
    private LocalFileSystem _lfs;

    private Logger _logger;

    private PdsDb _db;

    public BackgroundJobs(LocalFileSystem lfs, Logger logger, PdsDb db)
    {
        _lfs = lfs;
        _logger = logger;
        _db = db;
    }

    public void Start()
    {
        // run Job_UpdateLogLevel every 15 seconds
        System.Threading.Timer timer = new System.Threading.Timer(_ => Job_UpdateLogLevel(), null, TimeSpan.Zero, TimeSpan.FromSeconds(15));
    }


    private void Job_UpdateLogLevel()
    {
        string currentLevel = _logger.GetLogLevel();
        string newLevel = _db.GetLogLevel();

        if (string.Equals(currentLevel, newLevel, StringComparison.OrdinalIgnoreCase) == false)
        {
            _logger.LogInfo($"Updating log level from [{currentLevel}] to [{newLevel}]");
            _logger.SetLogLevel(newLevel);
        }
    }
}