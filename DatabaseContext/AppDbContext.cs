using Microsoft.EntityFrameworkCore;
using DatabaseContext.Models;
using Utilities;

namespace DatabaseContext
{
    public class AppDbContext : DbContext
    {
        public DbSet<Property> Properties { get; set; }
        public DbSet<PropertyDetails> PropertyDetails { get; set; }
        public DbSet<FailedItem> FailedItems { get; set; }
        public DbSet<Branch> Branches { get; set; }
        public DbSet<BasicListingUrl> NewListingUrls { get; set; }
        public DbSet<UpdateListingUrl> UpdateListingUrls { get; set; }
        public DbSet<UpdateListingDetails> UpdateListingDetails { get; set; }
        public DbSet<NewBranchUrl> NewBranchUrls { get; set; }

        public AppDbContext()
        {
            Database.SetCommandTimeout(600);
        }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            AppConfig appConfig = new AppConfig();
            options.UseSqlServer(appConfig.DbConnectionString);
        }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BasicListingUrl>().HasNoKey().ToView("vw_ListingUrl");
            modelBuilder.Entity<NewBranchUrl>().HasNoKey().ToView("vw_BranchUrl");
            modelBuilder.Entity<UpdateListingDetails>().HasNoKey().ToView("vw_UpdateDetails");
            modelBuilder.Entity<UpdateListingUrl>().HasNoKey();
        }   
    }
    public class BasicListingUrl
    {
        public int ListingSiteRef { get; set; }
        public string ListingUrl { get; set; }
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
    public class UpdateListingDetails
    {
        public string ListingUrl { get; set; }
        public int ListingSiteRef { get; set; }
        public string BranchUrl { get; set; }
    }
}
