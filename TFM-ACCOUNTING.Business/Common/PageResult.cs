namespace TFM_ACCOUNTING.Business.Common;

public sealed record PageResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
