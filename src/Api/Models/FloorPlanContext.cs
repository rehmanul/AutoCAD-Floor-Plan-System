using Microsoft.EntityFrameworkCore;

namespace FloorPlanAPI.Models
{
    public class FloorPlanContext : DbContext
    {
        public FloorPlanContext(DbContextOptions<FloorPlanContext> options) : base(options) { }
        public DbSet<ProcessingJob> Jobs { get; set; }
    }
}