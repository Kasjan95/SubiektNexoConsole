using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SubiektNexoConnector.Core.Application.Products
{
   public sealed record StockOperationDto
    (
        string DocumentNumber,
        DateTime DocumentDate,
        decimal Quantity,
        string OperationType
    );
}
