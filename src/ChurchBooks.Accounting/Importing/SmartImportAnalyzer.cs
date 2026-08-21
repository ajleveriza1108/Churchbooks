using System.Globalization;

namespace ChurchBooks.Accounting.Importing;

public sealed class SmartImportAnalyzer
{
    private const int PreviewLimit = 50;

    public ImportAnalysisResult Analyze(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);
        if (headers.Count == 0) throw new ArgumentException("At least one header is required.", nameof(headers));
        if (headers.Count > 100) throw new ArgumentException("Import files are limited to 100 columns.", nameof(headers));

        var profiles = headers.Select((header, index) => Profile(index, header, rows)).ToArray();
        var mappings = profiles.Select(x => new ImportColumnMapping(x.ColumnIndex, x.Header, x.SuggestedRole)).ToArray();
        var previews = rows.Take(PreviewLimit).Select((row, index) =>
            new ImportPreviewRow(index + 2, row, ImportDuplicateFingerprint.Compute(row, mappings), false)).ToArray();
        var warnings = BuildWarnings(profiles);
        return new ImportAnalysisResult(profiles, previews, warnings);
    }

    public static string BuildSourceSignature(IReadOnlyList<string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        return string.Join("|", headers.Select(NormalizeHeaderKey));
    }

    public static string NormalizeHeaderKey(string? value) =>
        new((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    public static int ScoreHeaderCandidate(IReadOnlyList<string> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (cells.Count == 0) return 0;
        var nonEmpty = cells.Count(x => !string.IsNullOrWhiteSpace(x));
        if (nonEmpty == 0) return 0;
        var recognized = cells.Select(NormalizeHeaderKey).Count(x => InferByHeader(x).Role != ImportColumnRole.Ignore);
        var unique = cells.Where(x => !string.IsNullOrWhiteSpace(x)).Select(NormalizeHeaderKey).Distinct(StringComparer.Ordinal).Count();
        var score = recognized * 10 + Math.Min(nonEmpty, 10) + Math.Min(unique, 10);
        if (recognized >= 2) score += 20;
        if (cells.Any(x => DateTime.TryParse(x, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))) score -= 5;
        return Math.Max(0, score);
    }

    private static IReadOnlyList<string> BuildWarnings(IReadOnlyList<ImportColumnProfile> profiles)
    {
        var warnings = new List<string>();
        var roles = profiles.Select(x => x.SuggestedRole).ToHashSet();
        var hasPersonIdentity = roles.Contains(ImportColumnRole.PersonName) || roles.Contains(ImportColumnRole.FirstName) || roles.Contains(ImportColumnRole.LastName) || roles.Contains(ImportColumnRole.MemberNumber) || roles.Contains(ImportColumnRole.Email) || roles.Contains(ImportColumnRole.ExternalPersonId);
        var hasMoney = roles.Contains(ImportColumnRole.Amount) || roles.Contains(ImportColumnRole.Debit) || roles.Contains(ImportColumnRole.Credit);
        if (!hasMoney && !hasPersonIdentity)
            warnings.Add("No financial amount or recognizable person identity columns were inferred. Review mappings before staging.");
        if (hasMoney && !roles.Contains(ImportColumnRole.Date))
            warnings.Add("No date column was inferred. Review mappings before staging.");
        return warnings;
    }

    private static ImportColumnProfile Profile(int index, string? rawHeader, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var header = (rawHeader ?? string.Empty).Trim();
        var normalized = NormalizeHeaderKey(header);
        var samples = rows.Select(r => index < r.Count ? r[index] : string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).Take(5).ToArray();
        var (role, confidence) = InferByHeader(normalized);
        if (role == ImportColumnRole.Ignore && LooksLikeDate(samples))
            return new ImportColumnProfile(index, header, ImportColumnRole.Date, 0.55m, samples);
        return new ImportColumnProfile(index, header, role, confidence, samples);
    }

    private static (ImportColumnRole Role, decimal Confidence) InferByHeader(string h) => h switch
    {
        "DATE" or "TRANSACTIONDATE" or "POSTINGDATE" or "POSTEDDATE" or "GIFTDATE" or "CONTRIBUTIONDATE" => (ImportColumnRole.Date, 0.98m),
        "AMOUNT" or "TRANSACTIONAMOUNT" or "NETAMOUNT" or "GIFTAMOUNT" or "CONTRIBUTIONAMOUNT" => (ImportColumnRole.Amount, 0.98m),
        "DEBIT" or "DEBITAMOUNT" or "WITHDRAWAL" or "WITHDRAWALS" => (ImportColumnRole.Debit, 0.98m),
        "CREDIT" or "CREDITAMOUNT" or "DEPOSIT" or "DEPOSITS" => (ImportColumnRole.Credit, 0.98m),
        "DESCRIPTION" or "DETAILS" or "TRANSACTIONDESCRIPTION" or "NARRATIVE" => (ImportColumnRole.Description, 0.95m),
        "REFERENCE" or "REFERENCEID" or "REF" or "CHECKNUMBER" or "CHEQUENUMBER" or "TRANSACTIONID" => (ImportColumnRole.Reference, 0.92m),
        "PAYEE" or "VENDOR" or "SUPPLIER" => (ImportColumnRole.Payee, 0.92m),
        "PERSON" or "PERSONNAME" or "MEMBERNAME" or "DONORNAME" or "FULLNAME" or "NAME" => (ImportColumnRole.PersonName, 0.94m),
        "MEMBERNUMBER" or "MEMBERNO" or "MEMBERID" or "DONORNUMBER" or "DONORNO" or "ENVELOPENUMBER" or "ENVELOPENO" => (ImportColumnRole.MemberNumber, 0.96m),
        "EXTERNALPERSONID" or "EXTERNALID" or "SOURCEPERSONID" or "CONTACTID" or "CONSTITUENTID" or "DONORID" => (ImportColumnRole.ExternalPersonId, 0.95m),
        "FIRSTNAME" or "GIVENNAME" or "FORENAME" => (ImportColumnRole.FirstName, 0.98m),
        "MIDDLENAME" or "MIDDLEINITIAL" => (ImportColumnRole.MiddleName, 0.94m),
        "LASTNAME" or "SURNAME" or "FAMILYNAME" => (ImportColumnRole.LastName, 0.98m),
        "PREFERREDNAME" or "NICKNAME" or "KNOWNAS" => (ImportColumnRole.PreferredName, 0.92m),
        "EMAIL" or "EMAILADDRESS" or "EMAIL1" => (ImportColumnRole.Email, 0.98m),
        "PHONE" or "PHONENUMBER" or "MOBILE" or "MOBILEPHONE" or "CELLPHONE" or "CONTACTNUMBER" => (ImportColumnRole.Phone, 0.96m),
        "HOUSEHOLD" or "HOUSEHOLDNAME" or "FAMILY" or "FAMILYHOUSEHOLD" => (ImportColumnRole.HouseholdName, 0.90m),
        "MEMBER" or "ISMEMBER" or "MEMBERSTATUS" => (ImportColumnRole.IsMember, 0.90m),
        "DONOR" or "ISDONOR" or "DONORSTATUS" or "GIVER" => (ImportColumnRole.IsDonor, 0.90m),
        "FUND" or "FUNDCODE" or "MINISTRYFUND" => (ImportColumnRole.FundCode, 0.94m),
        "GIVINGCATEGORY" or "CATEGORY" or "OFFERINGTYPE" or "CONTRIBUTIONTYPE" or "GIFTTYPE" => (ImportColumnRole.GivingCategoryCode, 0.90m),
        "BANKACCOUNT" or "FINANCIALACCOUNT" => (ImportColumnRole.BankAccount, 0.92m),
        "ACCOUNTCODE" or "GLACCOUNT" or "GENERALLEDGERACCOUNT" => (ImportColumnRole.AccountCode, 0.92m),
        "MEMO" or "NOTE" or "NOTES" or "COMMENT" or "COMMENTS" => (ImportColumnRole.Memo, 0.88m),
        _ => (ImportColumnRole.Ignore, 0m)
    };

    private static bool LooksLikeDate(IReadOnlyList<string> samples)
    {
        if (samples.Count == 0) return false;
        var parsed = samples.Count(value => DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out _) || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _));
        return parsed == samples.Count;
    }
}
