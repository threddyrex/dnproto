

namespace dnproto.ws;


/// <summary>
/// Options for querying actor information.
/// By default it enables two things: Bluesky for handle resolution,
/// and did document resolution. Though the caller can turn on/off any options.
/// When I'm using the client-side tooling (ResolveActorInfo) I tend
/// to turn on All, to see the responses for everything.
/// 
/// Added this recently, as part of some tightening up of resolutions in the PDS.
/// For now let's just use Bluesky for handle resolution.
/// 
/// </summary>
public class ActorQueryOptions
{
    public bool All { get; set; } = false;

    public bool ResolveHandleViaBluesky { get; set; } = true; // <-- default true

    public bool ResolveHandleViaDns { get; set; } = false;

    public bool ResolveHandleViaHttp { get; set; } = false;

    public bool ResolveDidDoc { get; set; } = true; // <-- default true
}