
namespace dnproto.tests.cli.commands;

using dnproto.cli.commands;

public class PostMentionTests
{

    [Fact]
    public void FindPostMentions_Null()
    {
        List<PostMention>? ret = PostMention.FindPostMentions(null);
        Assert.Null(ret);
    }

    [Fact]
    public void FindPostMentions_Empty()
    {
        List<PostMention>? ret = PostMention.FindPostMentions("");
        Assert.Null(ret);
    }

    [Fact]
    public void FindPostMentions_NoMentions()
    {
        List<PostMention>? ret = PostMention.FindPostMentions("this does not contain any mentions");
        Assert.NotNull(ret);
        Assert.Empty(ret);
    }

    [Fact]
    public void FindPostMentions_OneMentionMiddle()
    {
        List<PostMention>? ret = PostMention.FindPostMentions("this contains @one.mention in it");
        Assert.NotNull(ret);
        Assert.Single(ret);
        Assert.Equal("one.mention", ret[0].Handle);
        Assert.Equal(14, ret[0].ByteStart);
        Assert.Equal(26, ret[0].ByteEnd);
    }

    [Fact]
    public void FindPostMentions_OneMentionEnd()
    {
        List<PostMention>? ret = PostMention.FindPostMentions("this contains @one.mention");
        Assert.NotNull(ret);
        Assert.Single(ret);
        Assert.Equal("one.mention", ret[0].Handle);
        Assert.Equal(14, ret[0].ByteStart);
        Assert.Equal(26, ret[0].ByteEnd);
    }

    [Fact]
    public void FindPostMentions_OneMentionEndPeriod()
    {
        List<PostMention>? ret = PostMention.FindPostMentions("this contains @one.mention.");
        Assert.NotNull(ret);
        Assert.Single(ret);
        Assert.Equal("one.mention", ret[0].Handle);
        Assert.Equal(14, ret[0].ByteStart);
        Assert.Equal(26, ret[0].ByteEnd);
    }

    [Fact]
    public void FindPostMentions_OneMentionPeriodSpace()
    {
        List<PostMention>? ret = PostMention.FindPostMentions("this contains @one.mention. hey");
        Assert.NotNull(ret);
        Assert.Single(ret);
        Assert.Equal("one.mention", ret[0].Handle);
        Assert.Equal(14, ret[0].ByteStart);
        Assert.Equal(26, ret[0].ByteEnd);
    }

    
    [Fact]
    public void FindPostMentions_TwoMentions()
    {
        List<PostMention>? ret = PostMention.FindPostMentions("this contains @one.mention and @two.mention hey");
        Assert.NotNull(ret);
        Assert.Equal(2, ret.Count);
        Assert.Equal("one.mention", ret[0].Handle);
        Assert.Equal(14, ret[0].ByteStart);
        Assert.Equal(26, ret[0].ByteEnd);
        Assert.Equal("two.mention", ret[1].Handle);
        Assert.Equal(31, ret[1].ByteStart);
        Assert.Equal(43, ret[1].ByteEnd);
    }

        [Fact]
    public void FindPostMentions_TwoMentionsEnd()
    {
        List<PostMention>? ret = PostMention.FindPostMentions("this contains @one.mention and @two.mention");
        Assert.NotNull(ret);
        Assert.Equal(2, ret.Count);
        Assert.Equal("one.mention", ret[0].Handle);
        Assert.Equal(14, ret[0].ByteStart);
        Assert.Equal(26, ret[0].ByteEnd);
        Assert.Equal("two.mention", ret[1].Handle);
        Assert.Equal(31, ret[1].ByteStart);
        Assert.Equal(43, ret[1].ByteEnd);
    }

}