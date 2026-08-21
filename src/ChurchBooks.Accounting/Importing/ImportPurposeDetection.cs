namespace ChurchBooks.Accounting.Importing;

public sealed record ImportPurposeDetection(ImportPurpose Purpose, decimal Confidence, string Explanation)
{
    public bool IsConfident => Purpose != ImportPurpose.Unknown && Confidence >= 0.70m;
}
