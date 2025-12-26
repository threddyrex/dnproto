namespace dnproto.tests.uri;


using dnproto.uri;

public class AtUriTests
{

    [Fact]
    public void FromBskyPost_Correct()
    {
        var ret = AtUri.FromBskyPost("https://bsky.app/profile/did:example/post/123");
        Assert.NotNull(ret);
        Assert.Equal("did:example", ret.Authority);
        Assert.Equal("123", ret.Rkey);
    }

    [Fact]
    public void FromBskyPost_Null()
    {
        var ret = AtUri.FromBskyPost(null);
        Assert.Null(ret);
    }

    [Fact]
    public void FromBskyPost_NotAPost()
    {
        var ret = AtUri.FromBskyPost("https://bsky.app/fsadasdfasdf");
        Assert.Null(ret);
    }

    [Fact]
    public void FromAtUri_Correct()
    {
        var ret = AtUri.FromAtUri("at://did:plc:44ybard66vv44zksje25o7dz/app.bsky.feed.post/3jwdwj2ctlk26");
        Assert.NotNull(ret);
        Assert.Equal("did:plc:44ybard66vv44zksje25o7dz", ret.Authority);
        Assert.Equal("app.bsky.feed.post", ret.Collection);
        Assert.Equal("3jwdwj2ctlk26", ret.Rkey);
    }

    [Fact]
    public void FromAtUri_Null()
    {
        var ret = AtUri.FromAtUri(null);
        Assert.Null(ret);
    }


    [Fact]
    public void FromAtUri_NotAPost()
    {
        var ret = AtUri.FromAtUri("at://did:plc:44ybard66vv44zksje25o7dz");
        Assert.NotNull(ret);
        Assert.Equal("did:plc:44ybard66vv44zksje25o7dz", ret.Authority);
        Assert.Null(ret.Collection);
        Assert.Null(ret.Rkey);
    }

    [Fact]
    public void FromAtUri_MissingRkey()
    {
        var ret = AtUri.FromAtUri("at://did:plc:44ybard66vv44zksje25o7dz/app.bsky.feed.post");
        Assert.NotNull(ret);
        Assert.Equal("did:plc:44ybard66vv44zksje25o7dz", ret.Authority);
        Assert.Equal("app.bsky.feed.post", ret.Collection);
        Assert.Null(ret.Rkey);
    }

}
