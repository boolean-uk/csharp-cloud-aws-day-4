using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryService.Models
{
    public class Order
    {
        public int OrderId { get; set; }
        public string Product { get; set; }
        public int Quantity { get; set; }
    }
}
