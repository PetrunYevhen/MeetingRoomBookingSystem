using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MeetingRoomBooking.Api.Infrastructure.Persistence;

/// <summary>
/// Translates a losing concurrent insert into the name of the unique index/constraint it
/// violated, per ADR 0002's failure-translation rule: only a `DbUpdateException` that
/// identifies a *known* unique index becomes a domain conflict response; every other
/// database error is left alone and surfaces as a server failure.
/// </summary>
public static class SqlServerExceptions
{
    private const int DuplicateKeyIndexError = 2601;
    private const int DuplicateKeyConstraintError = 2627;
    private const int ForeignKeyReferenceError = 547;

    private static readonly Regex UniqueIndexNamePattern = new(
        "with unique index '(?<name>[^']+)'", RegexOptions.Compiled);

    private static readonly Regex UniqueConstraintNamePattern = new(
        "constraint '(?<name>[^']+)'", RegexOptions.Compiled);

    private static readonly Regex ForeignKeyNamePattern = new(
        "REFERENCE constraint \"(?<name>[^\"]+)\"", RegexOptions.Compiled);

    public static bool TryGetViolatedConstraintName(DbUpdateException exception, out string? constraintName)
    {
        constraintName = null;

        if (!TryGetSqlException(exception, out var sqlException) ||
            (sqlException.Number != DuplicateKeyIndexError && sqlException.Number != DuplicateKeyConstraintError))
        {
            return false;
        }

        var match = UniqueIndexNamePattern.Match(sqlException.Message);
        if (!match.Success)
        {
            match = UniqueConstraintNamePattern.Match(sqlException.Message);
        }

        if (!match.Success)
        {
            return false;
        }

        constraintName = match.Groups["name"].Value;
        return true;
    }

    /// <summary>
    /// Identifies the FK a blocked delete/update violated (SQL Server error 547, e.g. a
    /// `DeleteBehavior.Restrict` relationship refusing to orphan a child row). Distinct
    /// error and message format from the unique-key violations above.
    /// </summary>
    public static bool TryGetViolatedForeignKeyName(DbUpdateException exception, out string? foreignKeyName)
    {
        foreignKeyName = null;

        if (!TryGetSqlException(exception, out var sqlException) || sqlException.Number != ForeignKeyReferenceError)
        {
            return false;
        }

        var match = ForeignKeyNamePattern.Match(sqlException.Message);
        if (!match.Success)
        {
            return false;
        }

        foreignKeyName = match.Groups["name"].Value;
        return true;
    }

    private static bool TryGetSqlException(DbUpdateException exception, out SqlException sqlException)
    {
        if (exception.InnerException is SqlException inner)
        {
            sqlException = inner;
            return true;
        }

        sqlException = null!;
        return false;
    }
}
