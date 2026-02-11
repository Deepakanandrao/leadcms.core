// <copyright file="ContactEmailCommunicationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.RegularExpressions;
using LeadCMS.Configuration;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Infrastructure;
using LeadCMS.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LeadCMS.Services;

public class ContactEmailCommunicationService : IContactEmailCommunicationService
{
    private static readonly Regex OnWroteRegex = new("^\\s*On\\s+.+\\s+wrote\\s*:\\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex OriginalMessageRegex = new("^\\s*-{2,}\\s*Original Message\\s*-{2,}\\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ForwardedMessageRegex = new("^\\s*-{2,}\\s*Forwarded message\\s*-{2,}\\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HeaderFromRegex = new("^\\s*From\\s*:\\s+.+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HeaderSentRegex = new("^\\s*Sent\\s*:\\s+.+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HeaderToRegex = new("^\\s*To\\s*:\\s+.+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HeaderSubjectRegex = new("^\\s*Subject\\s*:\\s+.+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HeaderCcRegex = new("^\\s*Cc\\s*:\\s+.+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ReplySeparatorRegex = new("^\\s*_{5,}\\s*$", RegexOptions.Compiled);

    private static readonly Regex HtmlBlockquoteRegex = new("<blockquote[^>]*>[\\s\\S]*?</blockquote>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HtmlGmailQuoteRegex = new("<div\\s[^>]*class\\s*=\\s*[\"']gmail_quote[\"'][^>]*>[\\s\\S]*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HtmlOutlookAppendRegex = new("<div\\s[^>]*id\\s*=\\s*[\"']appendonsend[\"'][^>]*>[\\s\\S]*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HtmlYahooQuoteRegex = new("<div\\s[^>]*class\\s*=\\s*[\"']yahoo_quoted[\"'][^>]*>[\\s\\S]*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HtmlGmailExtraRegex = new("<div\\s[^>]*class\\s*=\\s*[\"']gmail_extra[\"'][^>]*>[\\s\\S]*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HtmlOutlookBorderSeparatorRegex = new("<div\\s[^>]*style\\s*=\\s*[\"'][^\"']*border-top\\s*:[^\"']*[\"'][^>]*>[\\s\\S]*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HtmlHrRegex = new("<hr[^>]*>[\\s\\S]*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HtmlOnWroteRegex = new("On\\s.+?wrote:\\s*</", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private readonly PgDbContext dbContext;
    private readonly ApiSettingsConfig apiSettings;

    public ContactEmailCommunicationService(PgDbContext dbContext, IOptions<ApiSettingsConfig> apiSettings)
    {
        this.dbContext = dbContext;
        this.apiSettings = apiSettings.Value;
    }

    public async Task<QueryResult<EmailLog>> GetCommunicationsAsync(int contactId, string queryString, bool applyDefaultOrder = true)
    {
        var rawQuery = NormalizeQueryString(queryString);
        if (applyDefaultOrder && !rawQuery.Contains("filter[order]", StringComparison.OrdinalIgnoreCase))
        {
            rawQuery = string.IsNullOrEmpty(rawQuery) ? "filter[order]=CreatedAt DESC" : $"{rawQuery}&filter[order]=CreatedAt DESC";
        }

        var queryCommands = QueryStringParser.Parse(System.Web.HttpUtility.UrlDecode(rawQuery));
        var queryBuilder = new QueryModelBuilder<EmailLog>(queryCommands, apiSettings.MaxListSize, dbContext);
        var baseQuery = dbContext.EmailLogs!
            .AsNoTracking()
            .Where(emailLog => emailLog.ContactId == contactId);

        var queryProvider = new DBQueryProvider<EmailLog>(baseQuery, queryBuilder);
        return await queryProvider.GetResult();
    }

    public async Task<EmailLog> GetCommunicationAsync(int contactId, int emailLogId)
    {
        var emailLog = await dbContext.EmailLogs!
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ContactId == contactId && e.Id == emailLogId);

        if (emailLog == null)
        {
            throw new EntityNotFoundException(typeof(EmailLog).Name, emailLogId.ToString());
        }

        return emailLog;
    }

    public string? PrepareBody(EmailLog emailLog)
    {
        if (!string.IsNullOrWhiteSpace(emailLog.HtmlBody))
        {
            return StripQuotedThreadHtml(emailLog.HtmlBody);
        }

        if (!string.IsNullOrWhiteSpace(emailLog.TextBody))
        {
            return StripQuotedThreadText(emailLog.TextBody);
        }

        return null;
    }

    public async Task<ContactEmailCommunicationStatsDto> GetStatsAsync(int contactId, DateTime? from, DateTime? to, EmailCommunicationStatsGroupBy groupBy)
    {
        var normalizedFrom = from.HasValue ? (DateTime?)NormalizeUtcDate(from.Value) : null;
        var normalizedTo = to.HasValue ? (DateTime?)NormalizeUtcDate(to.Value) : null;

        IQueryable<EmailLog> query = dbContext.EmailLogs!
            .AsNoTracking()
            .Where(emailLog => emailLog.ContactId == contactId);

        if (normalizedFrom.HasValue)
        {
            query = query.Where(emailLog => emailLog.CreatedAt >= normalizedFrom.Value);
        }

        if (normalizedTo.HasValue)
        {
            query = query.Where(emailLog => emailLog.CreatedAt <= normalizedTo.Value);
        }

        var emailLogs = await query
            .OrderBy(emailLog => emailLog.CreatedAt)
            .ToListAsync();

        var timeline = emailLogs
            .GroupBy(emailLog => GetPeriodStart(emailLog.CreatedAt, groupBy))
            .OrderBy(group => group.Key)
            .Select(group => new ContactEmailCommunicationTimelinePointDto
            {
                PeriodStart = group.Key,
                PeriodEnd = GetPeriodEnd(group.Key, groupBy),
                TotalCount = group.LongCount(),
                SentCount = group.LongCount(emailLog => emailLog.Status == EmailStatus.Sent),
                ReceivedCount = group.LongCount(emailLog => emailLog.Status == EmailStatus.Received),
                NotSentCount = group.LongCount(emailLog => emailLog.Status == EmailStatus.NotSent),
            })
            .ToList();

        return new ContactEmailCommunicationStatsDto
        {
            ContactId = contactId,
            From = normalizedFrom,
            To = normalizedTo,
            TotalCount = emailLogs.LongCount(),
            SentCount = emailLogs.LongCount(emailLog => emailLog.Status == EmailStatus.Sent),
            ReceivedCount = emailLogs.LongCount(emailLog => emailLog.Status == EmailStatus.Received),
            NotSentCount = emailLogs.LongCount(emailLog => emailLog.Status == EmailStatus.NotSent),
            FirstCommunicationAt = emailLogs.FirstOrDefault()?.CreatedAt,
            LastCommunicationAt = emailLogs.LastOrDefault()?.CreatedAt,
            Timeline = timeline,
        };
    }

    private static string StripQuotedThreadHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var result = html;
        result = HtmlGmailQuoteRegex.Replace(result, string.Empty);
        result = HtmlGmailExtraRegex.Replace(result, string.Empty);
        result = HtmlOutlookAppendRegex.Replace(result, string.Empty);
        result = HtmlYahooQuoteRegex.Replace(result, string.Empty);
        result = HtmlBlockquoteRegex.Replace(result, string.Empty);
        result = HtmlOutlookBorderSeparatorRegex.Replace(result, string.Empty);
        result = HtmlHrRegex.Replace(result, string.Empty);

        // Handle "On ... wrote:" pattern in HTML
        var onWroteMatch = HtmlOnWroteRegex.Match(result);
        if (onWroteMatch.Success)
        {
            result = result[..onWroteMatch.Index];
        }

        return result.Trim();
    }

    private static string StripQuotedThreadText(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        var normalized = body.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var cutoffIndex = lines.Length;

        for (var i = 0; i < lines.Length; i++)
        {
            var current = lines[i].Trim();
            if (IsThreadBoundary(lines, i, current))
            {
                cutoffIndex = i;
                break;
            }
        }

        var result = string.Join("\n", lines.Take(cutoffIndex));
        return result.TrimEnd();
    }

    private static bool IsThreadBoundary(string[] lines, int index, string current)
    {
        if (string.IsNullOrWhiteSpace(current))
        {
            return false;
        }

        if (current.StartsWith('>'))
        {
            return true;
        }

        if (OnWroteRegex.IsMatch(current)
            || OriginalMessageRegex.IsMatch(current)
            || ForwardedMessageRegex.IsMatch(current)
            || ReplySeparatorRegex.IsMatch(current))
        {
            return true;
        }

        if (HeaderFromRegex.IsMatch(current))
        {
            var nextLines = lines.Skip(index + 1).Take(5).Select(line => line.Trim()).Where(line => !string.IsNullOrWhiteSpace(line));
            if (nextLines.Any(line =>
                HeaderSentRegex.IsMatch(line)
                || HeaderToRegex.IsMatch(line)
                || HeaderSubjectRegex.IsMatch(line)
                || HeaderCcRegex.IsMatch(line)))
            {
                return true;
            }
        }

        if (HeaderSentRegex.IsMatch(current) && index > 0 && HeaderFromRegex.IsMatch(lines[index - 1].Trim()))
        {
            return true;
        }

        return false;
    }

    private static DateTime GetPeriodStart(DateTime timestamp, EmailCommunicationStatsGroupBy groupBy)
    {
        timestamp = NormalizeUtcDate(timestamp);

        return groupBy switch
        {
            EmailCommunicationStatsGroupBy.Day => timestamp.Date,
            EmailCommunicationStatsGroupBy.Week => GetWeekStart(timestamp),
            EmailCommunicationStatsGroupBy.Month => new DateTime(timestamp.Year, timestamp.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => timestamp.Date,
        };
    }

    private static DateTime GetPeriodEnd(DateTime periodStart, EmailCommunicationStatsGroupBy groupBy)
    {
        return groupBy switch
        {
            EmailCommunicationStatsGroupBy.Day => periodStart.AddDays(1),
            EmailCommunicationStatsGroupBy.Week => periodStart.AddDays(7),
            EmailCommunicationStatsGroupBy.Month => periodStart.AddMonths(1),
            _ => periodStart.AddDays(1),
        };
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        var daysToSubtract = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.Date.AddDays(-daysToSubtract);
    }

    private static DateTime NormalizeUtcDate(DateTime dateTime)
    {
        if (dateTime.Kind == DateTimeKind.Utc)
        {
            return dateTime;
        }

        if (dateTime.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        }

        return dateTime.ToUniversalTime();
    }

    private static string NormalizeQueryString(string queryString)
    {
        return queryString.Trim().TrimStart('?');
    }
}
