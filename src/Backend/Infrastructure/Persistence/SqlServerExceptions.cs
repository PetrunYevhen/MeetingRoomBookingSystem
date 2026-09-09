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

    private static readonly Regex UniqueIndexNamePattern = new(
        "with unique index '(?<name>[^']+)'", RegexOptions.Compiled);

    private static readonly Regex UniqueConstraintNamePattern = new(
        "constraint '(?<name>[^']+)'", RegexOptions.Compiled);

    public static bool TryGetViolatedConstraintName(DbUpdateException exception, out string? constraintName)
    {
        constraintName = null;

        if (exception.InnerException is not SqlException sqlException)
        {
            return false;
        }

        if (sqlException.Number != DuplicateKeyIndexError && sqlException.Number != DuplicateKeyConstraintError)
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
}
