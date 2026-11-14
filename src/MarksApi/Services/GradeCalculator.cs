namespace MarksApi.Services;

public sealed record MarkSummary(int Enrolled, int Graded, double? Average, int? Highest, int? Lowest, int Passed);

/// <summary>Pure grading rules (no database). Easy to unit test.</summary>
public static class GradeCalculator
{
    private static double Round(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static bool IsPassed(int? mark, int passMark) => mark.HasValue && mark.Value >= passMark;

    /// <summary>Average of graded marks only. Null (not 0) when nobody is graded.</summary>
    public static double? Average(IEnumerable<int?> marks)
    {
        var graded = marks.Where(m => m.HasValue).Select(m => m!.Value).ToList();
        return graded.Count == 0 ? null : Round(graded.Average());
    }

    /// <summary>sum(mark * creditHours) / sum(creditHours) over graded courses only. Never an average of averages.</summary>
    public static double? WeightedAverage(IEnumerable<(int? Mark, int CreditHours)> courses)
    {
        var graded = courses.Where(c => c.Mark.HasValue).ToList();
        var credits = graded.Sum(c => c.CreditHours);
        if (credits == 0) return null;
        var points = graded.Sum(c => (double)c.Mark!.Value * c.CreditHours);
        return Round(points / credits);
    }

    public static MarkSummary Summarize(IReadOnlyCollection<int?> marks, int passMark)
    {
        var graded = marks.Where(m => m.HasValue).Select(m => m!.Value).ToList();
        return new MarkSummary(
            Enrolled: marks.Count,
            Graded: graded.Count,
            Average: Average(marks),
            Highest: graded.Count == 0 ? null : graded.Max(),
            Lowest: graded.Count == 0 ? null : graded.Min(),
            Passed: graded.Count(m => m >= passMark));
    }
}
