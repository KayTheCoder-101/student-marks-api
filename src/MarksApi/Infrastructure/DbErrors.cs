using Npgsql;

namespace MarksApi.Infrastructure;

/// <summary>Recognises PostgreSQL constraint violations so they can be mapped to 404/409 instead of 500.</summary>
public static class DbErrors
{
    private static PostgresException? Pg(Exception ex) => ex as PostgresException ?? ex.InnerException as PostgresException;

    public static bool IsUniqueViolation(Exception ex) => Pg(ex)?.SqlState == PostgresErrorCodes.UniqueViolation;

    public static bool IsForeignKeyViolation(Exception ex) => Pg(ex)?.SqlState == PostgresErrorCodes.ForeignKeyViolation;

    /// <summary>ON DELETE RESTRICT raises 23001; NO ACTION raises 23503. Both mean "still referenced".</summary>
    public static bool IsStillReferenced(Exception ex) =>
        Pg(ex)?.SqlState is PostgresErrorCodes.RestrictViolation or PostgresErrorCodes.ForeignKeyViolation;
}
