using TFM_ACCOUNTING.Business.Common;

namespace TFM_ACCOUNTING.Business.Partners;

public sealed record PartnerQuery(string? Search = null, bool? IsActive = null, int Page = 1,
    int PageSize = 25, string SortBy = "Name", bool Descending = false, int? PartnerTypeId = null);

public sealed record PartnerTypeOption(int Id, string Name, bool IsActive)
{
    public string DisplayName => IsActive ? Name : Name + " (inaktív)";
}

public sealed record PartnerListItem(int Id, string Name, string? ShortName, string TaxNumber, string? City,
    bool IsActive, int PartnerTypeId, string PartnerTypeName)
{
    public string DisplayName => string.IsNullOrWhiteSpace(ShortName) ? Name : ShortName;
}

public sealed record PartnerDetails(int Id, string Name, string? ShortName, string TaxNumber,
    string? PostalCode, string? City, string? AddressLine, string? Notes, bool IsActive, byte[] RowVersion,
    int PartnerTypeId, string PartnerTypeName);

public sealed record SavePartnerRequest(int? Id, string Name, string? ShortName, string? TaxNumber,
    string? PostalCode, string? City, string? AddressLine, string? Notes, int PartnerTypeId, bool IsActive = true,
    bool GenerateInternalIdentifier = false, byte[]? RowVersion = null, bool ConfirmDuplicateName = false);

public interface IPartnerService
{
    Task<Response<PageResult<PartnerListItem>>> ListAsync(PartnerQuery query, CancellationToken cancellationToken = default);
    Task<Response<IReadOnlyList<PartnerTypeOption>>> ListTypesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<Response<PartnerDetails>> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<Response<PartnerDetails>> SaveAsync(SavePartnerRequest request, CancellationToken cancellationToken = default);
    Task<Response<PartnerDetails>> SetActiveAsync(int id, bool isActive, byte[] rowVersion, CancellationToken cancellationToken = default);
}
