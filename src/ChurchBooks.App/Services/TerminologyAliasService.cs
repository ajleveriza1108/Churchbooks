using ChurchBooks.Accounting.Setup;
using ChurchBooks.App.Models;

namespace ChurchBooks.App.Services;

public sealed class TerminologyAliasService
{
    private static readonly string[] FundAliases =
    {
        "fund", "funds", "restricted fund", "designated fund",
        "class", "classes", "quickbooks class", "tracking category", "xero tracking", "bucket",
        "fund balance", "fund transfer"
    };

    private static readonly string[] PeopleAliases =
    {
        "member", "members", "donor", "donors", "people", "person", "household", "households",
        "giving category", "giving categories", "contribution category", "offering category", "envelope"
    };

    private static readonly string[] GivingAliases =
    {
        "giving", "offering", "offerings", "contribution", "contributions", "tithe", "tithes",
        "service offering", "giving history", "donation history", "weekly offering", "monthly offering", "annual offering"
    };

    private static readonly string[] BankingAliases =
    {
        "bank", "banking", "bank account", "bank accounts", "deposit", "deposits",
        "deposit posting", "financial account", "financial accounts",
        "reconcile", "reconciliation", "bank reconciliation", "reconcile bank", "statement match"
    };

    private static readonly string[] ImportAliases =
    {
        "import", "smart import", "spreadsheet", "csv", "xls", "xlsx", "mapping", "column mapping"
    };

    private static readonly string[] ExpenseAliases =
    {
        "expense", "expenses", "vendor", "vendors", "purchase", "purchases", "supplier", "suppliers",
        "cash expense", "direct expense", "bank paid expense", "operating expense"
    };

    private static readonly string[] ReportAliases =
    {
        "report", "reports", "fund report", "fund balance report", "fund activity", "statement of activities",
        "financial report", "board report", "restricted fund report"
    };

    private static readonly string[] IntegrityAliases =
    {
        "integrity", "integrity center", "audit", "audit check", "health check", "accounting check",
        "data check", "fund check"
    };

    private static readonly string[] SetupAliases =
    {
        "setup", "settings", "personalization", "terminology", "labels", "terms"
    };

    private IReadOnlyList<CustomSearchAlias> _customAliases = Array.Empty<CustomSearchAlias>();
    private TerminologyCatalog _terminology = new();

    public void SetCustomAliases(IEnumerable<CustomSearchAlias>? aliases)
    {
        _customAliases = aliases?.ToArray() ?? Array.Empty<CustomSearchAlias>();
    }

    public void SetTerminology(TerminologyCatalog? terminology)
    {
        _terminology = terminology ?? new TerminologyCatalog();
    }

    public bool TryResolve(string? searchText, out WorkspaceSection section)
    {
        var normalized = searchText?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            section = WorkspaceSection.Dashboard;
            return false;
        }

        if (TryResolveCustomAlias(normalized, out section)
            || TryResolvePersonalizedTerm(normalized, out section)
            || ContainsAny(normalized, PeopleAliases, WorkspaceSection.People, out section)
            || ContainsAny(normalized, GivingAliases, WorkspaceSection.Giving, out section)
            || ContainsAny(normalized, BankingAliases, WorkspaceSection.Banking, out section)
            || ContainsAny(normalized, ImportAliases, WorkspaceSection.Import, out section)
            || ContainsAny(normalized, ExpenseAliases, WorkspaceSection.Expenses, out section)
            || ContainsAny(normalized, IntegrityAliases, WorkspaceSection.Integrity, out section)
            || ContainsAny(normalized, ReportAliases, WorkspaceSection.Reports, out section)
            || ContainsAny(normalized, FundAliases, WorkspaceSection.Funds, out section)
            || ContainsAny(normalized, SetupAliases, WorkspaceSection.Setup, out section))
        {
            return true;
        }

        section = WorkspaceSection.Dashboard;
        return false;
    }

    private bool TryResolveCustomAlias(string text, out WorkspaceSection section)
    {
        foreach (var alias in _customAliases)
        {
            if (!text.Contains(alias.AliasText, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            section = AreaToSection(alias.AreaKey);
            if (section != WorkspaceSection.Dashboard)
            {
                return true;
            }
        }

        section = WorkspaceSection.Dashboard;
        return false;
    }

    private bool TryResolvePersonalizedTerm(string text, out WorkspaceSection section)
    {
        foreach (var (key, target) in PersonalizedRoutes)
        {
            var term = _terminology.Get(key);
            if (text.Contains(term.Singular, StringComparison.OrdinalIgnoreCase)
                || text.Contains(term.Plural, StringComparison.OrdinalIgnoreCase))
            {
                section = target;
                return true;
            }
        }

        section = WorkspaceSection.Dashboard;
        return false;
    }

    private static bool ContainsAny(
        string text,
        IEnumerable<string> aliases,
        WorkspaceSection target,
        out WorkspaceSection section)
    {
        if (aliases.Any(alias => text.Contains(alias, StringComparison.OrdinalIgnoreCase)))
        {
            section = target;
            return true;
        }

        section = WorkspaceSection.Dashboard;
        return false;
    }

    private static WorkspaceSection AreaToSection(string areaKey) =>
        areaKey.Trim().ToLowerInvariant() switch
        {
            "people" => WorkspaceSection.People,
            "giving" => WorkspaceSection.Giving,
            "funds" => WorkspaceSection.Funds,
            "banking" => WorkspaceSection.Banking,
            "import" => WorkspaceSection.Import,
            "expenses" => WorkspaceSection.Expenses,
            "vendors" => WorkspaceSection.Expenses,
            "reports" => WorkspaceSection.Reports,
            "integrity" => WorkspaceSection.Integrity,
            "setup" => WorkspaceSection.Setup,
            _ => WorkspaceSection.Dashboard
        };

    private static IReadOnlyList<(string Key, WorkspaceSection Target)> PersonalizedRoutes { get; } =
        new (string Key, WorkspaceSection Target)[]
        {
            (TerminologyKeys.Member, WorkspaceSection.People),
            (TerminologyKeys.Donor, WorkspaceSection.People),
            (TerminologyKeys.Household, WorkspaceSection.People),
            (TerminologyKeys.GivingCategory, WorkspaceSection.People),
            (TerminologyKeys.Service, WorkspaceSection.Giving),
            (TerminologyKeys.Offering, WorkspaceSection.Giving),
            (TerminologyKeys.Fund, WorkspaceSection.Funds),
            (TerminologyKeys.BankAccount, WorkspaceSection.Banking),
            (TerminologyKeys.Deposit, WorkspaceSection.Banking)
        };
}
