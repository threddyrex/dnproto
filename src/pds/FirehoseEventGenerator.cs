
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

    public void GenerateFrame(string header_t, int header_op, JsonObject object2Json)
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
}