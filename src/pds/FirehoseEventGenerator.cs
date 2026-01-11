
using System.Text.Json.Nodes;
using dnproto.repo;

namespace dnproto.pds;

public class FirehoseEventGenerator
{
    private PdsDb _db;

    public FirehoseEventGenerator(PdsDb db)
    {
        _db = db;
    }

    public void GenerateFrame(string header_t, int header_op, JsonObject object2Json, byte[]? blocks = null)
    {

        //
        // OBJECT 1 (header)
        //
        var object1Json = new JsonObject()
        {
            ["t"] = header_t,
            ["op"] = header_op
        };
        DagCborObject object1DagCbor = DagCborObject.FromJsonString(object1Json.ToString());


        //
        // OBJECT 2 (message)
        //
        long sequenceNumber = _db.GetNewSequenceNumberForFirehose();
        string createdDate = FirehoseEvent.GetNewCreatedDate();


        object2Json["seq"] = sequenceNumber;
        object2Json["time"] = createdDate;

        var object2DagCbor = DagCborObject.FromJsonString(object2Json.ToString());

        if (blocks != null)
        {
            var object2Dict = (Dictionary<string, DagCborObject>)object2DagCbor.Value;
            object2Dict["blocks"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_BYTE_STRING, AdditionalInfo = 0, OriginalByte = 0 },
                Value = blocks
            };
        }

        //
        // insert into db
        //
        FirehoseEvent firehoseEvent = new FirehoseEvent()
        {
            SequenceNumber = sequenceNumber,
            CreatedDate = createdDate,
            Header_op = header_op,
            Header_t = header_t,
            Header_DagCborObject = object1DagCbor,
            Body_DagCborObject = object2DagCbor
        };
        _db.InsertFirehoseEvent(firehoseEvent); 
    }



    public void GenerateFrameWithBlocks(string header_t, int header_op, JsonObject object2Json, RepoHeader repoHeader, List<(CidV1 cid, DagCborObject dagCbor)> dagCborObjects)
    {
        //
        // Prepare the block stream
        //
        MemoryStream blockStream = new MemoryStream();

        repoHeader.WriteToStream(blockStream);

        foreach((CidV1 cid, DagCborObject dagCbor) in dagCborObjects)
        {
            DagCborObject.WriteToRepoStream(blockStream, cid, dagCbor);
        }


        //
        // Call GenerateFrame
        //
        GenerateFrame(header_t, header_op, object2Json, blockStream.ToArray());
    }
}