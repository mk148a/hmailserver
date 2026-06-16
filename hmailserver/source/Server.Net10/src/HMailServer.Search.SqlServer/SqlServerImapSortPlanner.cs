using System.Text;
using HMailServer.Core.Abstractions;

namespace HMailServer.Search.SqlServer;

public sealed class SqlServerImapSortPlanner
{
    public SqlSearchPlan Plan(ImapSortRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var searchRequest = request.SearchRequest;
        var headerTerms = searchRequest.GetHeaderTerms();
        var bodyTerms = searchRequest.GetBodyTerms();
        var anyTerms = searchRequest.GetAnyTerms();
        var needsFullText = headerTerms.Count > 0 || bodyTerms.Count > 0 || anyTerms.Count > 0;

        var sql = new StringBuilder("""
SELECT
    m.messageid,
    m.messageaccountid,
    m.messagefolderid,
    m.messageuid
FROM hm_messages AS m
LEFT JOIN hm_message_metadata AS md
    ON md.metadata_messageid = m.messageid

""");

        if (needsFullText)
        {
            sql.AppendLine("""
INNER JOIN hm_message_search_documents AS sd
    ON sd.messageid = m.messageid
""");
        }

        sql.AppendLine("""
WHERE
    m.messagetype = 2
    AND m.messageaccountid = @AccountId
    AND m.messagefolderid = @FolderId
""");

        var parameters = new Dictionary<string, object>
        {
            ["@AccountId"] = searchRequest.AccountId,
            ["@FolderId"] = searchRequest.FolderId
        };

        AddRangeFilter(sql, parameters, "m.messageuid", "@MinUid", searchRequest.MinUid, ">=");
        AddRangeFilter(sql, parameters, "m.messageuid", "@MaxUid", searchRequest.MaxUid, "<=");
        AddUidRangeFilter(sql, parameters, searchRequest.UidRanges);
        AddRangeFilter(sql, parameters, "m.messagesize", "@LargerThanBytes", searchRequest.LargerThanBytes, ">");
        AddRangeFilter(sql, parameters, "m.messagesize", "@SmallerThanBytes", searchRequest.SmallerThanBytes, "<");

        var requiredFlags = searchRequest.RequiredFlags;
        var forbiddenFlags = searchRequest.ForbiddenFlags;
        AddSessionRecentFilters(sql, parameters, searchRequest.SessionRecentUids, ref requiredFlags, ref forbiddenFlags);

        if (requiredFlags is { } requiredFlagsValue)
        {
            sql.AppendLine("    AND (m.messageflags & @RequiredFlags) = @RequiredFlags");
            parameters["@RequiredFlags"] = requiredFlagsValue;
        }

        if (forbiddenFlags is { } forbiddenFlagsValue)
        {
            sql.AppendLine("    AND (m.messageflags & @ForbiddenFlags) = 0");
            parameters["@ForbiddenFlags"] = forbiddenFlagsValue;
        }

        if (searchRequest.Since is { } since)
        {
            sql.AppendLine("    AND m.messagecreatetime >= @Since");
            parameters["@Since"] = since.ToDateTime(TimeOnly.MinValue);
        }

        if (searchRequest.Before is { } before)
        {
            sql.AppendLine("    AND m.messagecreatetime < @Before");
            parameters["@Before"] = before.ToDateTime(TimeOnly.MinValue);
        }

        AddFullTextPredicates(sql, parameters, "sd.search_header", "@HeaderText", headerTerms);
        AddFullTextPredicates(sql, parameters, "sd.search_body", "@BodyText", bodyTerms);
        AddFullTextPredicates(sql, parameters, "sd.search_combined", "@AnyText", anyTerms);
        AddOrderBy(sql, request.Criteria);
        return new SqlSearchPlan(sql.ToString(), parameters);
    }

    private static void AddOrderBy(StringBuilder sql, IReadOnlyList<ImapSortCriterion> criteria)
    {
        if (criteria.Count == 0)
        {
            throw new InvalidOperationException("SORT criteria list is empty.");
        }

        sql.Append("ORDER BY ");
        for (var index = 0; index < criteria.Count; index++)
        {
            if (index > 0)
            {
                sql.Append(", ");
            }

            var criterion = criteria[index];
            sql.Append(GetSortExpression(criterion.Key))
                .Append(criterion.Descending ? " DESC" : " ASC");
        }

        sql.AppendLine(", m.messageuid ASC;");
    }

    private static string GetSortExpression(ImapSortKey key) =>
        key switch
        {
            ImapSortKey.Arrival => "m.messagecreatetime",
            ImapSortKey.Cc => "LOWER(COALESCE(md.metadata_cc, N''))",
            ImapSortKey.Date => "COALESCE(md.metadata_dateutc, m.messagecreatetime)",
            ImapSortKey.From => "LOWER(COALESCE(md.metadata_from, N''))",
            ImapSortKey.Size => "m.messagesize",
            ImapSortKey.Subject => "LOWER(COALESCE(md.metadata_subject, N''))",
            ImapSortKey.To => "LOWER(COALESCE(md.metadata_to, N''))",
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown IMAP SORT criterion.")
        };

    private static void AddRangeFilter(
        StringBuilder sql,
        IDictionary<string, object> parameters,
        string column,
        string name,
        long? value,
        string comparison)
    {
        if (value is null)
        {
            return;
        }

        sql.Append("    AND ")
            .Append(column)
            .Append(' ')
            .Append(comparison)
            .Append(' ')
            .AppendLine(name);
        parameters[name] = value.Value;
    }

    private static void AddUidRangeFilter(
        StringBuilder sql,
        IDictionary<string, object> parameters,
        IReadOnlyList<ImapIdRange>? uidRanges)
    {
        if (uidRanges is null || uidRanges.Count == 0)
        {
            return;
        }

        sql.Append("    AND (");

        for (var index = 0; index < uidRanges.Count; index++)
        {
            var range = uidRanges[index];
            if (index > 0)
            {
                sql.Append(" OR ");
            }

            var startName = $"@UidRangeStart{index}";

            if (range.Start is null && range.End is null)
            {
                sql.Append("1 = 1");
                continue;
            }

            if (range.Start is null)
            {
                var endOnlyName = $"@UidRangeEnd{index}";
                parameters[endOnlyName] = range.End!.Value;
                sql.Append("m.messageuid <= ").Append(endOnlyName);
                continue;
            }

            parameters[startName] = range.Start.Value;

            if (range.End is null)
            {
                sql.Append("m.messageuid >= ").Append(startName);
                continue;
            }

            if (range.IsSingle)
            {
                sql.Append("m.messageuid = ").Append(startName);
                continue;
            }

            var endName = $"@UidRangeEnd{index}";
            parameters[endName] = range.End.Value;
            sql.Append("m.messageuid BETWEEN ")
                .Append(startName)
                .Append(" AND ")
                .Append(endName);
        }

        sql.AppendLine(")");
    }

    private static void AddSessionRecentFilters(
        StringBuilder sql,
        IDictionary<string, object> parameters,
        IReadOnlySet<long>? sessionRecentUids,
        ref byte? requiredFlags,
        ref byte? forbiddenFlags)
    {
        if (sessionRecentUids is null)
        {
            return;
        }

        var requiredRecent = HasFlag(requiredFlags, ImapMessageFlags.Recent);
        var forbiddenRecent = HasFlag(forbiddenFlags, ImapMessageFlags.Recent);
        requiredFlags = RemoveFlag(requiredFlags, ImapMessageFlags.Recent);
        forbiddenFlags = RemoveFlag(forbiddenFlags, ImapMessageFlags.Recent);

        if (requiredRecent)
        {
            AddUidListFilter(sql, parameters, sessionRecentUids, include: true);
        }

        if (forbiddenRecent)
        {
            AddUidListFilter(sql, parameters, sessionRecentUids, include: false);
        }
    }

    private static void AddUidListFilter(
        StringBuilder sql,
        IDictionary<string, object> parameters,
        IReadOnlySet<long> uids,
        bool include)
    {
        if (uids.Count == 0)
        {
            if (include)
            {
                sql.AppendLine("    AND 1 = 0");
            }

            return;
        }

        sql.Append(include ? "    AND m.messageuid IN (" : "    AND m.messageuid NOT IN (");
        var index = 0;
        foreach (var uid in uids.OrderBy(static value => value))
        {
            if (index > 0)
            {
                sql.Append(", ");
            }

            var name = $"@SessionRecentUid{index}";
            sql.Append(name);
            parameters[name] = uid;
            index++;
        }

        sql.AppendLine(")");
    }

    private static bool HasFlag(byte? flags, byte flag) =>
        flags is { } value && (value & flag) == flag;

    private static byte? RemoveFlag(byte? flags, byte flag)
    {
        if (flags is not { } value)
        {
            return null;
        }

        var reduced = (byte)(value & ~flag);
        return reduced == 0 ? null : reduced;
    }

    private static void AddFullTextPredicates(
        StringBuilder sql,
        IDictionary<string, object> parameters,
        string column,
        string parameterPrefix,
        IReadOnlyList<string> values)
    {
        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index];
            var name = $"{parameterPrefix}{index}";

            sql.Append("    AND CONTAINS(")
                .Append(column)
                .Append(", ")
                .Append(name)
                .AppendLine(")");
            parameters[name] = EscapeContainsTerm(value);
        }
    }

    private static string EscapeContainsTerm(string value)
    {
        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}
