using MarksApi.Infrastructure;
using Xunit;

namespace MarksApi.Tests;

public class PagingTests
{
    [Fact]
    public void Normalize_UsesDefaults_WhenValuesAreMissing()
    {
        Assert.Equal((1, 10), Paging.Normalize(null, null));
    }

    [Fact]
    public void Normalize_CorrectsNegativeAndZeroValues()
    {
        Assert.Equal((1, 10), Paging.Normalize(0, -5));
    }

    [Fact]
    public void Normalize_EnforcesTheMaximumPageSize()
    {
        Assert.Equal(50, Paging.Normalize(1, 10_000).PageSize);
    }

    [Fact]
    public void ParseSort_ReadsDescendingPrefix()
    {
        var (field, desc) = Paging.ParseSort("-FullName", "id", ["id", "fullname"]);
        Assert.Equal("fullname", field);
        Assert.True(desc);
    }

    [Fact]
    public void ParseSort_UsesDefault_WhenEmpty()
    {
        Assert.Equal(("id", false), Paging.ParseSort(null, "id", ["id"]));
    }

    [Fact]
    public void ParseSort_RejectsUnknownFields()
    {
        var ex = Assert.Throws<RequestValidationException>(() => Paging.ParseSort("password", "id", ["id"]));
        Assert.Contains("sort", ex.Errors.Keys);
    }

    [Fact]
    public void EscapeLike_EscapesWildcards()
    {
        Assert.Equal("50\\%\\_x", Paging.EscapeLike("50%_x"));
    }
}
