namespace SubiektNexoConnector.Core.Application.AdditionalFields.GetFieldsType
{
    public sealed record GetFieldsTypeQuery(AdditionalFieldTarget Target);

    public enum AdditionalFieldTarget
    {
        Product,
        Party
    }
}
