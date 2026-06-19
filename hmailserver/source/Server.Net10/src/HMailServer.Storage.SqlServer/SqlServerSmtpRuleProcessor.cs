using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;
using MimeKit;
using MimeKit.Utils;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerSmtpRuleProcessor : ISmtpRuleProcessor, ISmtpAccountRuleProcessor
{
    public const string SelectRulesForAccountSql = """
SELECT
    ruleid,
    rulename,
    ruleuseand,
    rulesortorder
FROM hm_rules
WHERE
    ruleaccountid = @AccountId
    AND ruleactive <> 0
ORDER BY rulesortorder ASC, ruleid ASC;

SELECT
    c.criteriaid,
    c.criteriaruleid,
    c.criteriausepredefined,
    c.criteriapredefinedfield,
    c.criteriaheadername,
    c.criteriamatchtype,
    c.criteriamatchvalue
FROM hm_rule_criterias c
INNER JOIN hm_rules r ON r.ruleid = c.criteriaruleid
WHERE
    r.ruleaccountid = @AccountId
    AND r.ruleactive <> 0
ORDER BY c.criteriaruleid ASC, c.criteriaid ASC;

SELECT
    a.actionid,
    a.actionruleid,
    a.actiontype,
    a.actionimapfolder,
    a.actionsubject,
    a.actionfromname,
    a.actionfromaddress,
    a.actionto,
    a.actionbody,
    a.actionfilename,
    a.actionsortorder,
    a.actionscriptfunction,
    a.actionheader,
    a.actionvalue,
    a.actionrouteid,
    a.actionabortspamflagged
FROM hm_rule_actions a
INNER JOIN hm_rules r ON r.ruleid = a.actionruleid
WHERE
    r.ruleaccountid = @AccountId
    AND r.ruleactive <> 0
ORDER BY a.actionruleid ASC, a.actionsortorder ASC, a.actionid ASC;
""";

    public const string SelectGlobalRulesSql = SelectRulesForAccountSql;

    private const string RuleLoopCountHeader = "X-hMailServer-LoopCount";
    private const string CopyRuleHeader = "X-CopyRule";

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly SmtpRuleProcessorOptions _options;
    private readonly ISmtpRuleScriptExecutor? _scriptExecutor;

    public SqlServerSmtpRuleProcessor(
        SqlServerConnectionFactory connectionFactory,
        SmtpRuleProcessorOptions? options = null)
        : this(connectionFactory, options, scriptExecutor: null)
    {
    }

    public SqlServerSmtpRuleProcessor(
        SqlServerConnectionFactory connectionFactory,
        SmtpRuleProcessorOptions? options,
        ISmtpRuleScriptExecutor? scriptExecutor)
    {
        _connectionFactory = connectionFactory;
        _options = options ?? new SmtpRuleProcessorOptions();
        _scriptExecutor = scriptExecutor;
        ArgumentOutOfRangeException.ThrowIfNegative(_options.RuleLoopLimit);
    }

    public async ValueTask<SmtpRuleProcessingResult> ProcessAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rules = await LoadRulesForAccountAsync(accountId: 0, cancellationToken).ConfigureAwait(false);
        return ApplyRules(request, rules, cancellationToken, _options.RuleLoopLimit, _scriptExecutor);
    }

    public async ValueTask<SmtpRuleProcessingResult> ProcessAccountAsync(
        int accountId,
        SmtpReceiveRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(accountId);
        ArgumentNullException.ThrowIfNull(request);

        var rules = await LoadRulesForAccountAsync(accountId, cancellationToken).ConfigureAwait(false);
        return ApplyRules(request, rules, cancellationToken, _options.RuleLoopLimit, _scriptExecutor, accountId);
    }

    public static SmtpRuleProcessingResult ApplyRules(
        SmtpReceiveRequest request,
        IReadOnlyList<SmtpRuleDefinition> rules,
        CancellationToken cancellationToken = default,
        int ruleLoopLimit = 5,
        ISmtpRuleScriptExecutor? scriptExecutor = null,
        int accountId = 0)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(rules);

        if (rules.Count == 0)
        {
            return SmtpRuleProcessingResult.Continue(request.MessageData);
        }

        var context = RuleMessageContext.Create(request);
        var dropMessage = false;
        string? moveToImapFolder = null;
        var forcedRouteId = 0;
        string? bindToAddress = null;
        var generatedMessages = new List<SmtpRuleGeneratedMessage>();
        var messageCopyOperations = new List<ScriptMessageCopyOperation>();
        var continueRuleProcessing = true;
        foreach (var rule in rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!continueRuleProcessing)
            {
                break;
            }

            if (!RuleMatches(context, rule))
            {
                continue;
            }

            foreach (var action in rule.Actions)
            {
                switch (action.Type)
                {
                    case SmtpRuleActionType.Delete:
                        dropMessage = true;
                        break;

                    case SmtpRuleActionType.StopRuleProcessing:
                        continueRuleProcessing = false;
                        break;

                    case SmtpRuleActionType.SetHeaderValue:
                        context.SetHeader(action.HeaderName, action.Value);
                        break;

                    case SmtpRuleActionType.ScriptFunction:
                        if (scriptExecutor is not null &&
                            !string.IsNullOrWhiteSpace(action.ScriptFunction))
                        {
                            var scriptResult = scriptExecutor.Execute(
                                new SmtpRuleScriptExecutionRequest(
                                    action.ScriptFunction,
                                    rule.Id,
                                    rule.Name,
                                    accountId,
                                    request.MailFrom,
                                    request.Recipients,
                                    context.GetMessageData()),
                                cancellationToken);
                            if (!scriptResult.Accepted)
                            {
                                return SmtpRuleProcessingResult.Failure(
                                    string.IsNullOrWhiteSpace(scriptResult.FailureResponse)
                                        ? "451 Requested action aborted: local error in processing"
                                        : scriptResult.FailureResponse,
                                    scriptResult.MessageData ?? context.GetMessageData());
                            }

                            if (scriptResult.MessageData is not null)
                            {
                                context.ReplaceMessageData(scriptResult.MessageData);
                            }

                            if (scriptResult.DropMessage)
                            {
                                dropMessage = true;
                            }

                            if (accountId > 0 &&
                                scriptResult.MessageCopyOperations is { Count: > 0 } copyOperations)
                            {
                                messageCopyOperations.AddRange(copyOperations);
                            }
                        }

                        break;

                    case SmtpRuleActionType.MoveToImapFolder:
                        if (!string.IsNullOrWhiteSpace(action.ImapFolder))
                        {
                            moveToImapFolder = action.ImapFolder;
                        }

                        break;

                    case SmtpRuleActionType.Forward:
                        if (context.CanGenerate(ruleLoopLimit) &&
                            TryCreateRuleRecipients(action.To, out var forwardRecipients))
                        {
                            generatedMessages.Add(
                                new SmtpRuleGeneratedMessage(
                                    request.MailFrom,
                                    forwardRecipients,
                                    context.CreateGeneratedMessageData()));
                        }

                        break;

                    case SmtpRuleActionType.Reply:
                        if (context.TryCreateReply(action, ruleLoopLimit, out var replyMessage))
                        {
                            generatedMessages.Add(replyMessage);
                        }

                        break;

                    case SmtpRuleActionType.CreateCopy:
                        if (context.CanGenerate(ruleLoopLimit) &&
                            request.Recipients.Count > 0)
                        {
                            generatedMessages.Add(
                                new SmtpRuleGeneratedMessage(
                                    request.MailFrom,
                                    request.Recipients.ToArray(),
                                    context.CreateGeneratedMessageData(copyRuleName: rule.Name)));
                        }

                        break;

                    case SmtpRuleActionType.SendUsingRoute:
                        if (action.RouteId is > 0 and <= int.MaxValue)
                        {
                            forcedRouteId = (int)action.RouteId;
                        }

                        break;

                    case SmtpRuleActionType.BindToAddress:
                        if (!string.IsNullOrWhiteSpace(action.Value))
                        {
                            bindToAddress = action.Value.Trim();
                        }

                        break;
                }
            }
        }

        var messageData = context.GetMessageData();
        return dropMessage
            ? SmtpRuleProcessingResult.Drop(messageData, generatedMessages, messageCopyOperations)
            : SmtpRuleProcessingResult.Continue(
                messageData,
                moveToImapFolder,
                generatedMessages,
                forcedRouteId,
                bindToAddress,
                messageCopyOperations);
    }

    private async ValueTask<IReadOnlyList<SmtpRuleDefinition>> LoadRulesForAccountAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(SelectRulesForAccountSql, connection);
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var ruleBuilders = new List<RuleBuilder>();
        var ruleBuildersById = new Dictionary<long, RuleBuilder>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var builder = new RuleBuilder(
                ReadInt32(reader, "ruleid"),
                ReadString(reader, "rulename"),
                ReadByte(reader, "ruleuseand") != 0,
                ReadInt32(reader, "rulesortorder"));
            ruleBuilders.Add(builder);
            ruleBuildersById.Add(builder.Id, builder);
        }

        await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var ruleId = ReadInt32(reader, "criteriaruleid");
            if (!ruleBuildersById.TryGetValue(ruleId, out var builder))
            {
                continue;
            }

            builder.Criteria.Add(
                    new SmtpRuleCriterion(
                    ReadInt32(reader, "criteriaid"),
                    ReadByte(reader, "criteriausepredefined") != 0,
                    (SmtpRuleCriteriaField)ReadByte(reader, "criteriapredefinedfield"),
                    ReadString(reader, "criteriaheadername"),
                    (SmtpRuleMatchType)ReadByte(reader, "criteriamatchtype"),
                    ReadString(reader, "criteriamatchvalue")));
        }

        await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var ruleId = ReadInt32(reader, "actionruleid");
            if (!ruleBuildersById.TryGetValue(ruleId, out var builder))
            {
                continue;
            }

            builder.Actions.Add(
                    new SmtpRuleAction(
                    ReadInt32(reader, "actionid"),
                    (SmtpRuleActionType)ReadByte(reader, "actiontype"),
                    ReadInt32(reader, "actionsortorder"),
                    ReadString(reader, "actionimapfolder"),
                    ReadString(reader, "actionsubject"),
                    ReadString(reader, "actionfromname"),
                    ReadString(reader, "actionfromaddress"),
                    ReadString(reader, "actionto"),
                    ReadString(reader, "actionbody"),
                    ReadString(reader, "actionfilename"),
                    ReadString(reader, "actionscriptfunction"),
                    ReadString(reader, "actionheader"),
                    ReadString(reader, "actionvalue"),
                    ReadInt32(reader, "actionrouteid"),
                    ReadByte(reader, "actionabortspamflagged") != 0));
        }

        return ruleBuilders
            .Select(static builder => builder.Build())
            .ToArray();
    }

    private static int ReadInt32(
        SqlDataReader reader,
        string name) =>
        reader.GetInt32(reader.GetOrdinal(name));

    private static byte ReadByte(
        SqlDataReader reader,
        string name) =>
        reader.GetByte(reader.GetOrdinal(name));

    private static string ReadString(
        SqlDataReader reader,
        string name) =>
        reader.GetString(reader.GetOrdinal(name));

    private static bool RuleMatches(
        RuleMessageContext context,
        SmtpRuleDefinition rule)
    {
        if (rule.Criteria.Count == 0)
        {
            return false;
        }

        return rule.UseAnd
            ? rule.Criteria.All(criteria => CriterionMatches(context, criteria))
            : rule.Criteria.Any(criteria => CriterionMatches(context, criteria));
    }

    private static bool CriterionMatches(
        RuleMessageContext context,
        SmtpRuleCriterion criterion)
    {
        var fieldValue = criterion.UsePredefinedField
            ? context.GetPredefinedField(criterion.PredefinedField)
            : context.GetHeader(criterion.HeaderName);
        return TestMatch(criterion.MatchValue, criterion.MatchType, fieldValue);
    }

    private static bool TestMatch(
        string matchValue,
        SmtpRuleMatchType matchType,
        string testValue)
    {
        switch (matchType)
        {
            case SmtpRuleMatchType.Equals:
                return string.Equals(matchValue, testValue, StringComparison.OrdinalIgnoreCase);

            case SmtpRuleMatchType.NotEquals:
                return !string.Equals(matchValue, testValue, StringComparison.OrdinalIgnoreCase);

            case SmtpRuleMatchType.Contains:
                return testValue.Contains(matchValue, StringComparison.OrdinalIgnoreCase);

            case SmtpRuleMatchType.NotContains:
                return !testValue.Contains(matchValue, StringComparison.OrdinalIgnoreCase);

            case SmtpRuleMatchType.LessThan:
                return TryParseRuleDouble(matchValue, out var lessThanMatch) &&
                    TryParseRuleDouble(testValue, out var lessThanValue) &&
                    lessThanMatch > lessThanValue;

            case SmtpRuleMatchType.GreaterThan:
                return TryParseRuleDouble(matchValue, out var greaterThanMatch) &&
                    TryParseRuleDouble(testValue, out var greaterThanValue) &&
                    greaterThanMatch < greaterThanValue;

            case SmtpRuleMatchType.MatchesRegex:
                return Regex.IsMatch(
                    testValue,
                    matchValue,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(250));

            case SmtpRuleMatchType.Wildcard:
                return WildcardMatches(matchValue, testValue);

            default:
                return false;
        }
    }

    private static bool TryParseRuleDouble(
        string value,
        out double number)
    {
        return double.TryParse(
            value.Replace(',', '.'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out number);
    }

    private static bool WildcardMatches(
        string pattern,
        string value)
    {
        var builder = new StringBuilder();
        builder.Append('^');
        foreach (var ch in pattern)
        {
            builder.Append(ch switch
            {
                '*' => ".*",
                '?' => ".",
                _ => Regex.Escape(ch.ToString())
            });
        }

        builder.Append('$');
        return Regex.IsMatch(
            value,
            builder.ToString(),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(250));
    }

    private static bool TryCreateRuleRecipients(
        string recipientList,
        out IReadOnlyList<SmtpResolvedRecipient> recipients)
    {
        recipients = [];
        if (string.IsNullOrWhiteSpace(recipientList))
        {
            return false;
        }

        var addresses = new List<string>();
        try
        {
            var parsed = InternetAddressList.Parse(recipientList.Replace(';', ','));
            addresses.AddRange(parsed.Mailboxes.Select(static mailbox => mailbox.Address));
        }
        catch (ParseException)
        {
            addresses.AddRange(
                recipientList
                    .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(static value => value.Trim('<', '>', ' ')));
        }

        recipients = addresses
            .Where(static address => !string.IsNullOrWhiteSpace(address))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static address => new SmtpResolvedRecipient(
                address,
                address,
                LocalAccountId: 0,
                IsLocal: false))
            .ToArray();
        return recipients.Count > 0;
    }

    private sealed class RuleBuilder
    {
        public RuleBuilder(
            long id,
            string name,
            bool useAnd,
            int sortOrder)
        {
            Id = id;
            Name = name;
            UseAnd = useAnd;
            SortOrder = sortOrder;
        }

        public long Id { get; }

        public string Name { get; }

        public bool UseAnd { get; }

        public int SortOrder { get; }

        public List<SmtpRuleCriterion> Criteria { get; } = [];

        public List<SmtpRuleAction> Actions { get; } = [];

        public SmtpRuleDefinition Build() =>
            new(Id, Name, UseAnd, SortOrder, Criteria.ToArray(), Actions.ToArray());
    }

    private sealed class RuleMessageContext
    {
        private readonly SmtpReceiveRequest _request;
        private byte[]? _messageDataOverride;
        private MimeMessage? _message;
        private bool _messageChanged;

        private RuleMessageContext(
            SmtpReceiveRequest request,
            MimeMessage? message)
        {
            _request = request;
            _message = message;
        }

        public static RuleMessageContext Create(SmtpReceiveRequest request)
        {
            MimeMessage? message = null;
            try
            {
                using var stream = new MemoryStream(request.MessageData, writable: false);
                message = MimeMessage.Load(stream);
            }
            catch (FormatException)
            {
            }

            return new RuleMessageContext(request, message);
        }

        public string GetPredefinedField(SmtpRuleCriteriaField field)
        {
            return field switch
            {
                SmtpRuleCriteriaField.From => _message?.From.ToString() ?? _request.MailFrom,
                SmtpRuleCriteriaField.To => _message?.To.ToString() ?? string.Empty,
                SmtpRuleCriteriaField.Cc => _message?.Cc.ToString() ?? string.Empty,
                SmtpRuleCriteriaField.Subject => _message?.Subject ?? string.Empty,
                SmtpRuleCriteriaField.Body => string.Concat(_message?.TextBody ?? string.Empty, _message?.HtmlBody ?? string.Empty),
                SmtpRuleCriteriaField.MessageSize => GetMessageData().LongLength.ToString(CultureInfo.InvariantCulture),
                SmtpRuleCriteriaField.RecipientList => string.Join(';', _request.Recipients.Select(static recipient => recipient.Address)),
                SmtpRuleCriteriaField.DeliveryAttempts => "1",
                _ => string.Empty
            };
        }

        public string GetHeader(string headerName)
        {
            if (string.IsNullOrWhiteSpace(headerName))
            {
                return string.Empty;
            }

            return _message?.Headers[headerName] ?? string.Empty;
        }

        public bool CanGenerate(int ruleLoopLimit) =>
            ruleLoopLimit <= 0 || GetRuleLoopCount() < ruleLoopLimit;

        public bool TryCreateReply(
            SmtpRuleAction action,
            int ruleLoopLimit,
            out SmtpRuleGeneratedMessage generatedMessage)
        {
            generatedMessage = default!;
            if (!CanGenerate(ruleLoopLimit) || IsAutoSubmitted())
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(_request.MailFrom) ||
                !TryCreateRuleRecipients(_request.MailFrom, out var replyRecipients))
            {
                return false;
            }

            var reply = new MimeMessage
            {
                MessageId = MimeUtils.GenerateMessageId(),
                Date = DateTimeOffset.UtcNow,
                Subject = action.Subject ?? string.Empty,
                Body = new TextPart("plain") { Text = action.Body ?? string.Empty }
            };

            if (TryCreateMailbox(action.FromName, action.FromAddress, out var fromAddress))
            {
                reply.From.Add(fromAddress);
            }

            if (TryCreateMailbox(string.Empty, replyRecipients[0].Address, out var toAddress))
            {
                reply.To.Add(toAddress);
            }

            SetHeader(reply, "Auto-Submitted", "auto-replied");
            SetHeader(reply, RuleLoopCountHeader, (GetRuleLoopCount() + 1).ToString(CultureInfo.InvariantCulture));

            using var output = new MemoryStream();
            reply.WriteTo(output);
            generatedMessage = new SmtpRuleGeneratedMessage(
                string.IsNullOrWhiteSpace(action.FromAddress) ? string.Empty : action.FromAddress.Trim(),
                replyRecipients,
                output.ToArray());
            return true;
        }

        public void SetHeader(
            string headerName,
            string value)
        {
            if (string.IsNullOrWhiteSpace(headerName))
            {
                return;
            }

            if (_message is null)
            {
                return;
            }

            _message.Headers.RemoveAll(headerName);
            _message.Headers.Add(headerName, value);
            _messageChanged = true;
        }

        public void ReplaceMessageData(byte[] messageData)
        {
            _messageDataOverride = messageData;
            _messageChanged = false;
            try
            {
                using var stream = new MemoryStream(messageData, writable: false);
                _message = MimeMessage.Load(stream);
            }
            catch (FormatException)
            {
                _message = null;
            }
        }

        public byte[] GetMessageData()
        {
            if (!_messageChanged || _message is null)
            {
                return _messageDataOverride ?? _request.MessageData;
            }

            using var output = new MemoryStream();
            _message.WriteTo(output);
            return output.ToArray();
        }

        public byte[] CreateGeneratedMessageData(string? copyRuleName = null)
        {
            using var input = new MemoryStream(GetMessageData(), writable: false);
            var message = MimeMessage.Load(input);
            SetHeader(message, RuleLoopCountHeader, (GetRuleLoopCount() + 1).ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(copyRuleName))
            {
                SetHeader(message, CopyRuleHeader, copyRuleName);
            }

            using var output = new MemoryStream();
            message.WriteTo(output);
            return output.ToArray();
        }

        private int GetRuleLoopCount()
        {
            var value = GetHeader(RuleLoopCountHeader);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var loopCount)
                ? loopCount
                : 0;
        }

        private bool IsAutoSubmitted()
        {
            var value = GetHeader("Auto-Submitted");
            return !string.IsNullOrWhiteSpace(value) &&
                   !string.Equals(value.Trim(), "no", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryCreateMailbox(
            string displayName,
            string address,
            out MailboxAddress mailbox)
        {
            mailbox = default!;
            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            try
            {
                mailbox = new MailboxAddress(displayName ?? string.Empty, address.Trim());
                return true;
            }
            catch (ParseException)
            {
                return false;
            }
        }

        private static void SetHeader(
            MimeMessage message,
            string headerName,
            string value)
        {
            message.Headers.RemoveAll(headerName);
            message.Headers.Add(headerName, value);
        }
    }
}
