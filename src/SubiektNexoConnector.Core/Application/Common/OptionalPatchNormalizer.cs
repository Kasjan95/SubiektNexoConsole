using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;

namespace SubiektNexoConnector.Core.Application.Common;

public static class OptionalPatchNormalizer
{
    public static Optional<string> RequiredText(
        Optional<string> field,
        string fieldName)
    {
        if (!field.HasValue)
            return field;

        if (field.Value is null)
            throw new InvalidOperationException($"{fieldName} cannot be null.");

        var normalizedValue = field.Value.Trim();
        if (normalizedValue.Length == 0)
            throw new InvalidOperationException($"{fieldName} cannot be empty.");

        return new Optional<string>(normalizedValue);
    }

    public static Optional<string?> OptionalText(Optional<string?> field)
    {
        if (!field.HasValue)
            return field;

        return string.IsNullOrWhiteSpace(field.Value)
            ? new Optional<string?>(null)
            : new Optional<string?>(field.Value.Trim());
    }

    public static Optional<IReadOnlyCollection<int>> PositiveDistinctIds(
        Optional<IReadOnlyCollection<int>> field,
        string fieldName)
    {
        if (!field.HasValue)
            return field;

        if (field.Value is null)
            throw new InvalidOperationException($"{fieldName} cannot be null.");

        if (field.Value.Any(id => id <= 0))
            throw new InvalidOperationException($"{fieldName} must contain positive values.");

        return new Optional<IReadOnlyCollection<int>>(field.Value.Distinct().ToList());
    }

    public static Optional<IReadOnlyCollection<AdditionalFieldValueDto>> AdditionalFields(
        Optional<IReadOnlyCollection<AdditionalFieldValueDto>> fields,
        string fieldName)
    {
        if (!fields.HasValue)
            return fields;

        if (fields.Value is null)
            throw new InvalidOperationException($"{fieldName} cannot be null.");

        if (fields.Value.Any(field => string.IsNullOrWhiteSpace(field.Id)))
            throw new InvalidOperationException($"{fieldName} cannot contain an empty field id.");

        if (fields.Value.GroupBy(field => field.Id, StringComparer.Ordinal).Any(group => group.Count() > 1))
            throw new InvalidOperationException($"{fieldName} cannot contain duplicate field ids.");

        return new Optional<IReadOnlyCollection<AdditionalFieldValueDto>>(fields.Value.ToList());
    }

    public static Optional<FlagAssignmentDto?> Flag(Optional<FlagAssignmentDto?> flag)
    {
        if (!flag.HasValue || flag.Value is null)
            return flag;

        if (flag.Value.Id <= 0)
            throw new InvalidOperationException("Flag id must be positive.");

        var comment = string.IsNullOrWhiteSpace(flag.Value.Comment)
            ? null
            : flag.Value.Comment.Trim();

        return new Optional<FlagAssignmentDto?>(new FlagAssignmentDto(flag.Value.Id, comment));
    }
}
