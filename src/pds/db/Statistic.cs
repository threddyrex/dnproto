

namespace dnproto.pds;

public class StatisticKey
{
    public required string Name { get; set; }
    public required string IpAddress { get; set; }
    public required string UserAgent { get; set; }    
}

public class Statistic
{
    public required string Name { get; set; }
    public required string IpAddress { get; set; }
    public required string UserAgent { get; set; }    
    public required long Value { get; set; }
    public required string LastUpdatedDate { get; set; }
}