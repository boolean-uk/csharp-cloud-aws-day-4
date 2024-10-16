using InventoryService.Models;
using Microsoft.EntityFrameworkCore;

namespace OrderService.Context
{
    public class OrderDbContext : DbContext
    {
        public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }

        public DbSet<Order> Orders { get; set; }
    }
}
