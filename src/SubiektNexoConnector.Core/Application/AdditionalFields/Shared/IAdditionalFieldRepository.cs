using SubiektNexoConnector.Core.Application.AdditionalFields.GetFieldsType;

namespace SubiektNexoConnector.Core.Application.AdditionalFields.Shared
{
    public interface IAdditionalFieldRepository
    {
        AdditionalFieldsDefinitionDto GetFieldsType(GetFieldsTypeQuery query);
    }
}
