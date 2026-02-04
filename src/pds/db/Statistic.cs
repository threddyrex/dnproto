

namespace dnproto.pds;

public class Statistic
{
    public required string Name { get; set; }
    public required string UserKey { get; set; }
    public required long Value { get; set; }
    public required string LastUpdatedDate { get; set; }
    public double LastUpdateMinutesAgo => (DateTime.UtcNow - DateTime.Parse(LastUpdatedDate)).TotalMinutes;
}