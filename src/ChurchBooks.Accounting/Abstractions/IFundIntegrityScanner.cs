using ChurchBooks.Accounting.Integrity;

namespace ChurchBooks.Accounting.Abstractions;

public interface IFundIntegrityScanner
{
    Task<FundIntegrityReport> ScanAsync(CancellationToken cancellationToken = default);
}
