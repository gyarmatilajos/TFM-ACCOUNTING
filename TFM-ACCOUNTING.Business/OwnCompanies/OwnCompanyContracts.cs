using TFM_ACCOUNTING.Business.Common;

namespace TFM_ACCOUNTING.Business.OwnCompanies;

public sealed record OwnCompanyQuery(string? Search = null, bool? IsActive = null, int Page = 1,
    int PageSize = 25, string SortBy = "Name", bool Descending = false);

public sealed record OwnCompanyListItem(int Id, string Name, string? ShortName, string TaxNumber, string? City, bool IsActive)
{
    public string DisplayName => string.IsNullOrWhiteSpace(ShortName) ? Name : ShortName;
}

public sealed record OwnCompanyDetails(int Id, string Name, string? ShortName, string TaxNumber,
    string? PostalCode, string? City, string? AddressLine, string? Notes, bool IsActive, byte[] RowVersion);

public sealed record SaveOwnCompanyRequest(int? Id, string Name, string? ShortName, string? TaxNumber,
    string? PostalCode, string? City, string? AddressLine, string? Notes, bool IsActive = true,
    bool GenerateInternalIdentifier = false, byte[]? RowVersion = null);

public interface IOwnCompanyService
{
    Task<Response<PageResult<OwnCompanyListItem>>> ListAsync(OwnCompanyQuery query, CancellationToken cancellationToken = default);
    Task<Response<OwnCompanyDetails>> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<Response<OwnCompanyDetails>> SaveAsync(SaveOwnCompanyRequest request, CancellationToken cancellationToken = default);
    Task<Response<OwnCompanyDetails>> SetActiveAsync(int id, bool isActive, byte[] rowVersion, CancellationToken cancellationToken = default);
}
