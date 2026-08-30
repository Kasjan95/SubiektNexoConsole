namespace SubiektNexoConnector.Api.Configuration;

public sealed class PaginationOptions
{
    public const string SectionName = "Api:Pagination";
    public const int AbsoluteMaxPageSize = 1000;

    public int DefaultPageSize { get; set; } = 100;
    public int MaxPageSize { get; set; } = 1000;

    public void Validate()
    {
        if (MaxPageSize < 1 || MaxPageSize > AbsoluteMaxPageSize)
            throw new InvalidOperationException($"Api:Pagination:MaxPageSize must be between 1 and {AbsoluteMaxPageSize}.");

        if (DefaultPageSize < 1 || DefaultPageSize > MaxPageSize)
            throw new InvalidOperationException("Api:Pagination:DefaultPageSize must be between 1 and MaxPageSize.");

    }

    public bool TryResolve(
        int? requestedPage,
        int? requestedPageSize,
        out PaginationParameters parameters,
        out Dictionary<string, string[]> errors)
    {
        var page = requestedPage ?? 1;
        var pageSize = requestedPageSize ?? DefaultPageSize;
        errors = new Dictionary<string, string[]>();

        if (page < 1)
            errors["page"] = ["Page must be greater than or equal to 1."];

        if (pageSize < 1 || pageSize > MaxPageSize)
            errors["pageSize"] = [$"Page size must be between 1 and {MaxPageSize}."];

        if (errors.Count == 0 && ((long)(page - 1) * pageSize) > int.MaxValue)
            errors["page"] = ["The requested page is too large."];

        parameters = new PaginationParameters(page, pageSize);
        return errors.Count == 0;
    }
}

public sealed record PaginationParameters(int Page, int PageSize);
