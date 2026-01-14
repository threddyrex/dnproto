


namespace dnproto.mst;

public class MstNode
{
    public required int KeyDepth;

    public MstNode? LeftTree = null;

    public required List<MstEntry> Entries;


    #region EQUALITY

    public override bool Equals(object? obj)
    {
        if (obj is not MstNode other)
            return false;

        if (KeyDepth != other.KeyDepth)
            return false;

        if (LeftTree is null != other.LeftTree is null)
            return false;

        if (LeftTree is not null && !LeftTree.Equals(other.LeftTree))
            return false;

        if (Entries.Count != other.Entries.Count)
            return false;

        for (int i = 0; i < Entries.Count; i++)
        {
            if (!Entries[i].Equals(other.Entries[i]))
                return false;
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(KeyDepth);
        hash.Add(LeftTree);
        foreach (var entry in Entries)
        {
            hash.Add(entry);
        }
        return hash.ToHashCode();
    }
    
    #endregion

}