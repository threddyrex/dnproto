


namespace dnproto.mst;


public class MstEntry
{
    public required string Key;

    public required string Value;

    public MstNode? RightTree = null;


    #region EQUALITY

    public override bool Equals(object? obj)
    {
        if (obj is not MstEntry other)
            return false;

        if (Key != other.Key)
            return false;

        if (Value != other.Value)
            return false;

        if (RightTree is null != other.RightTree is null)
            return false;

        if (RightTree is not null && !RightTree.Equals(other.RightTree))
            return false;

        return true;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Key, Value, RightTree);
    }

    #endregion
}