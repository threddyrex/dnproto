

using dnproto.repo;

namespace dnproto.pds;

/// <summary>
/// Represents one event (frame) in the firehose stream.
/// A frame contains two dag-cbor objects: a header and a body.
/// Those two objects are stored in this class, along with some
/// metadata such as the sequence number and creation date.
/// 
/// https://atproto.com/specs/event-stream
/// 
/// </summary>
public class FirehoseEvent
{
    /// <summary>
    /// The sequence number of the event in the firehose stream.
    /// </summary>
    public required long SequenceNumber;

    /// <summary>
    /// The creation date of the event.
    /// UTC
    /// </summary>
    public required string CreatedDate;

    /// <summary>
    /// 
    /// "op"
    /// 
    /// Operation type. 
    /// 1 for a good message, -1 for error.
    /// </summary>
    public required int Header_op;

    /// <summary>
    /// 
    /// "t"
    /// 
    /// The type of the header.
    /// ex: "#commit"
    /// Omitted if "op" is -1 (error)
    /// </summary>
    public string? Header_t;

    /// <summary>
    /// The full header object in DAG-CBOR format.
    /// (Object 1 of 2)
    /// </summary>
    public required DagCborObject Header_DagCborObject;

    /// <summary>
    /// The full body object in DAG-CBOR format.
    /// (Object 2 of 2)
    /// </summary>
    public DagCborObject? Body_DagCborObject;


}