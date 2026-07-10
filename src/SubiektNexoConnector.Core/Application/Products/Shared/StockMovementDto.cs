using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed record StockMovementDto
    (
        int DocumentCount,
        decimal TotalQuantity,
        IReadOnlyCollection<StockOperationDto> Items
    );
}
