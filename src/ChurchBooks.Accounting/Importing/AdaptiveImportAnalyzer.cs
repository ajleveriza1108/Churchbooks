namespace ChurchBooks.Accounting.Importing;

public sealed class AdaptiveImportAnalyzer
{
    private readonly SmartImportAnalyzer _columnAnalyzer = new();

    public AdaptiveImportAnalysis Analyze(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var columnAnalysis = _columnAnalyzer.Analyze(headers, rows);
        var roles = columnAnalysis.Columns.Select(x => x.SuggestedRole).ToHashSet();
        var scores = new Dictionary<ImportPurpose, int>
        {
            [ImportPurpose.PeopleDirectory] = ScorePeople(roles),
            [ImportPurpose.Giving] = ScoreGiving(roles),
            [ImportPurpose.BankStatement] = ScoreBank(roles),
            [ImportPurpose.GeneralLedger] = ScoreLedger(roles)
        };
        var ordered = scores.OrderByDescending(x => x.Value).ToArray();
        var best = ordered[0];
        var second = ordered[1];
        var purpose = best.Value < 4 || best.Value == second.Value ? ImportPurpose.Unknown : best.Key;
        var confidence = purpose == ImportPurpose.Unknown
            ? Math.Min(0.55m, best.Value / 10m)
            : Math.Min(0.98m, 0.55m + best.Value * 0.055m + Math.Max(0, best.Value - second.Value) * 0.025m);
        var explanation = purpose == ImportPurpose.Unknown
            ? "The file layout is unfamiliar or ambiguous. Review the suggested mappings before staging."
            : $"Detected {PurposeLabel(purpose)} from the mapped column pattern. Review is still available.";
        return new AdaptiveImportAnalysis(
            columnAnalysis,
            new ImportPurposeDetection(purpose, confidence, explanation),
            SmartImportAnalyzer.BuildSourceSignature(headers));
    }

    private static int ScorePeople(IReadOnlySet<ImportColumnRole> roles)
    {
        var score = 0;
        if (roles.Contains(ImportColumnRole.FirstName)) score += 3;
        if (roles.Contains(ImportColumnRole.LastName)) score += 3;
        if (roles.Contains(ImportColumnRole.PersonName)) score += 4;
        if (roles.Contains(ImportColumnRole.MemberNumber)) score += 2;
        if (roles.Contains(ImportColumnRole.ExternalPersonId)) score += 2;
        if (roles.Contains(ImportColumnRole.Email)) score += 2;
        if (roles.Contains(ImportColumnRole.Phone)) score += 1;
        if (roles.Contains(ImportColumnRole.IsMember) || roles.Contains(ImportColumnRole.IsDonor)) score += 2;
        if (HasMoney(roles)) score -= 3;
        return score;
    }

    private static int ScoreGiving(IReadOnlySet<ImportColumnRole> roles)
    {
        var score = HasMoney(roles) ? 4 : 0;
        if (roles.Contains(ImportColumnRole.Date)) score += 2;
        if (roles.Contains(ImportColumnRole.GivingCategoryCode)) score += 3;
        if (roles.Contains(ImportColumnRole.FundCode)) score += 2;
        if (roles.Contains(ImportColumnRole.PersonName) || roles.Contains(ImportColumnRole.MemberNumber) || roles.Contains(ImportColumnRole.ExternalPersonId)) score += 2;
        if (roles.Contains(ImportColumnRole.AccountCode)) score -= 2;
        return score;
    }

    private static int ScoreBank(IReadOnlySet<ImportColumnRole> roles)
    {
        var score = HasMoney(roles) ? 3 : 0;
        if (roles.Contains(ImportColumnRole.Date)) score += 2;
        if (roles.Contains(ImportColumnRole.Description)) score += 2;
        if (roles.Contains(ImportColumnRole.Reference)) score += 1;
        if (roles.Contains(ImportColumnRole.AccountCode)) score -= 4;
        if (roles.Contains(ImportColumnRole.GivingCategoryCode)) score -= 3;
        return score;
    }

    private static int ScoreLedger(IReadOnlySet<ImportColumnRole> roles)
    {
        var score = HasMoney(roles) ? 3 : 0;
        if (roles.Contains(ImportColumnRole.Date)) score += 2;
        if (roles.Contains(ImportColumnRole.AccountCode)) score += 5;
        if (roles.Contains(ImportColumnRole.Reference)) score += 1;
        if (roles.Contains(ImportColumnRole.FundCode)) score += 1;
        return score;
    }

    private static bool HasMoney(IReadOnlySet<ImportColumnRole> roles) =>
        roles.Contains(ImportColumnRole.Amount) || roles.Contains(ImportColumnRole.Debit) || roles.Contains(ImportColumnRole.Credit);

    private static string PurposeLabel(ImportPurpose purpose) => purpose switch
    {
        ImportPurpose.PeopleDirectory => "a People Directory",
        ImportPurpose.Giving => "Giving detail",
        ImportPurpose.BankStatement => "a Bank Statement",
        ImportPurpose.GeneralLedger => "General Ledger detail",
        _ => "an unknown layout"
    };
}
