namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed class ProductDeletionFailedException : Exception
    {
        public ProductDeletionFailedException(string message) : base(message)
        {
        }
    }
}
