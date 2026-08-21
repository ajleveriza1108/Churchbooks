using ChurchBooks.Accounting.People;

namespace ChurchBooks.Accounting.Importing;

public sealed record PersonImportResolution(
    PersonImportCandidate Candidate,
    PersonImportResolutionStatus Status,
    PersonProfile? ExistingPerson,
    string Reason)
{
    public bool CanRegisterNew => Status == PersonImportResolutionStatus.NewPerson && Candidate.HasUsableName;
}
