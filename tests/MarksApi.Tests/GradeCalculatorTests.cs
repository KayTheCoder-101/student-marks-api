using MarksApi.Services;
using Xunit;

namespace MarksApi.Tests;

public class GradeCalculatorTests
{
    [Fact]
    public void Average_IsNull_WhenNobodyIsGraded()
    {
        Assert.Null(GradeCalculator.Average(new int?[] { null, null }));
        Assert.Null(GradeCalculator.Average(Array.Empty<int?>()));
    }

    [Fact]
    public void Average_IgnoresUngradedEnrollments()
    {
        Assert.Equal(75.0, GradeCalculator.Average(new int?[] { 70, null, 80 }));
    }

    [Fact]
    public void WeightedAverage_MatchesTheExampleInTheSpecification()
    {
        // CS401: 3 credit hours, mark 80. MA201: 2 credit hours, mark 60. (80*3 + 60*2) / 5 = 72
        var result = GradeCalculator.WeightedAverage(new (int?, int)[] { (80, 3), (60, 2) });
        Assert.Equal(72.0, result);
    }

    [Fact]
    public void WeightedAverage_IsNotAnAverageOfAverages()
    {
        // 100 in a 1-credit course and 0 in a 4-credit course: weighted = 20, plain average of marks = 50.
        var result = GradeCalculator.WeightedAverage(new (int?, int)[] { (100, 1), (0, 4) });
        Assert.Equal(20.0, result);
    }

    [Fact]
    public void WeightedAverage_SkipsUngradedCourses()
    {
        var result = GradeCalculator.WeightedAverage(new (int?, int)[] { (80, 3), (null, 4) });
        Assert.Equal(80.0, result);
    }

    [Fact]
    public void WeightedAverage_IsNull_WhenNothingIsGraded()
    {
        Assert.Null(GradeCalculator.WeightedAverage(new (int?, int)[] { (null, 3) }));
    }

    [Theory]
    [InlineData(49, false)]
    [InlineData(50, true)]
    [InlineData(100, true)]
    [InlineData(null, false)]
    public void IsPassed_UsesGreaterThanOrEqualToThePassMark(int? mark, bool expected)
    {
        Assert.Equal(expected, GradeCalculator.IsPassed(mark, 50));
    }

    [Fact]
    public void Summarize_MatchesTheCourseStatsExample()
    {
        // Three students enrolled, two graded with 70 and 80.
        var s = GradeCalculator.Summarize(new int?[] { 70, 80, null }, passMark: 50);

        Assert.Equal(3, s.Enrolled);
        Assert.Equal(2, s.Graded);
        Assert.Equal(75.0, s.Average);
        Assert.Equal(80, s.Highest);
        Assert.Equal(70, s.Lowest);
        Assert.Equal(2, s.Passed);
    }

    [Fact]
    public void Summarize_HasNullStatistics_WhenNobodyIsGraded()
    {
        var s = GradeCalculator.Summarize(new int?[] { null, null }, passMark: 50);

        Assert.Equal(2, s.Enrolled);
        Assert.Equal(0, s.Graded);
        Assert.Null(s.Average);
        Assert.Null(s.Highest);
        Assert.Null(s.Lowest);
        Assert.Equal(0, s.Passed);
    }
}
