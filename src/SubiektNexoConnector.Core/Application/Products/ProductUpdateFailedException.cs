namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed class ProductUpdateFailedException : Exception
    {
        public ProductUpdateFailedException(string message) : base(message)
        {
        }
    }
}
