

public class ActorQueryOptions
{
    public bool All { get; set; } = false;

    public bool ResolveHandleViaBluesky { get; set; } = true; // <-- default true

    public bool ResolveHandleViaDns { get; set; } = false;

    public bool ResolveHandleViaHttp { get; set; } = false;

    public bool ResolveDidDoc { get; set; } = true; // <-- default true
}