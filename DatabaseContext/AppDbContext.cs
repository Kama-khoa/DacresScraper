using Microsoft.EntityFrameworkCore;
using DatabaseContext.Models;

namespace DatabaseContext
{
    public class AppDbContext : DbContext
    {
        public DbSet<Property> Properties { get; set; }
        public DbSet<PropertyDetails> PropertyDetails { get; set; }
        public DbSet<FailedItem> FailedItems { get; set; }
        public DbSet<Branch> Branches { get; set; }
        public IQueryable<BasicListingUrl> NewListingUrls { get; set; }
        public IQueryable<UpdateListingUrl> UpdateListingUrls { get; set; }
        public IQueryable<NewBranchUrl> NewBranchUrls { get; set; }

        public AppDbContext()
        {
            Database.SetCommandTimeout(600);
        }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlServer("Server=Khoa-Laptop;Database=RealEstateScraperDb;TrustServerCertificate=True;Trusted_Connection=True;MultipleActiveResultSets=true");
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BasicListingUrl>().HasNoKey().ToView("vm_ListingUrl");
            modelBuilder.Entity<NewBranchUrl>().HasNoKey().ToView("vm_BranchUrl");
        }   
    }
    public class BasicListingUrl
    {
        public int ListingSiteRef { get; set; }
        public string ListingUrl { get; set; }
        public string Address { get; set; }
        public bool CommercialListing { get; set; }
    }
    public class UpdateListingUrl
    {
        public int PropertyId { get; set; }
        public string ListingUrl { get; set; }
    }
    public class NewBranchUrl
    {
        public string BranchUrl { get; set; }
        public string BranchName { get; set; }
    }
}
