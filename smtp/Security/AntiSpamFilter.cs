using MimeKit;

namespace EmailHost.Smtp.Security;

public class SpamCheckResult
{
    public double Score { get; set; }
    public bool IsSpam => Score >= 5.0;
    public bool ShouldReject => Score >= 9.0;
    public List<string> MatchedRules { get; set; } = new();
}

public class AntiSpamFilter
{
    private static readonly string[] SpamTriggers =
    {
        "viagra", "cialis", "casino", "lottery", "winner", "inheritance",
        "million dollars", "bitcoin investment", "crypto profit", "urgent wire transfer",
        "verify your bank account", "free cash", "100% free guaranteed", "act now!",
        "congratulations you won", "western union transfer"
    };

    public SpamCheckResult Analyze(MimeMessage message, string spfVerdict)
    {
        var result = new SpamCheckResult();
        double score = 0.0;

        // 1. Missing or suspicious headers
        if (string.IsNullOrWhiteSpace(message.Subject))
        {
            score += 1.0;
            result.MatchedRules.Add("EMPTY_SUBJECT (+1.0)");
        }
        else
        {
            var subjectLower = message.Subject.ToLowerInvariant();
            foreach (var trigger in SpamTriggers)
            {
                if (subjectLower.Contains(trigger))
                {
                    score += 2.5;
                    result.MatchedRules.Add($"SPAM_TRIGGER_SUBJECT: '{trigger}' (+2.5)");
                }
            }

            // High uppercase ratio in subject
            var upperCount = message.Subject.Count(char.IsUpper);
            if (message.Subject.Length > 10 && (double)upperCount / message.Subject.Length > 0.6)
            {
                score += 1.5;
                result.MatchedRules.Add("EXCESSIVE_UPPERCASE_SUBJECT (+1.5)");
            }
        }

        // 2. Check SPF verdict impact
        if (spfVerdict == "Fail")
        {
            score += 3.5;
            result.MatchedRules.Add("SPF_FAIL (+3.5)");
        }
        else if (spfVerdict == "SoftFail")
        {
            score += 1.5;
            result.MatchedRules.Add("SPF_SOFTFAIL (+1.5)");
        }

        // 3. Body text heuristics
        var bodyText = message.TextBody?.ToLowerInvariant() ?? string.Empty;
        if (!string.IsNullOrEmpty(bodyText))
        {
            foreach (var trigger in SpamTriggers)
            {
                if (bodyText.Contains(trigger))
                {
                    score += 1.5;
                    result.MatchedRules.Add($"SPAM_TRIGGER_BODY: '{trigger}' (+1.5)");
                }
            }
        }

        // 4. Mismatched From / ReplyTo
        if (message.ReplyTo.Count > 0 && message.From.Count > 0)
        {
            var fromDomain = message.From.Mailboxes.FirstOrDefault()?.Domain;
            var replyDomain = message.ReplyTo.Mailboxes.FirstOrDefault()?.Domain;
            if (fromDomain != null && replyDomain != null && !fromDomain.Equals(replyDomain, StringComparison.OrdinalIgnoreCase))
            {
                score += 1.0;
                result.MatchedRules.Add("REPLY_TO_DOMAIN_MISMATCH (+1.0)");
            }
        }

        result.Score = Math.Round(score, 2);
        return result;
    }
}

