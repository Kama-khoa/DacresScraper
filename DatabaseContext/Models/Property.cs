namespace DatabaseContext.Models
{
    public class Property
    {
        public int Id { get; set; }
        public int ListingSiteRef { get; set; }
        public string Address { get; set; }
        public string PostcodeDistrict { get; set; }
        public string ListingUrl { get; set; }
        
        public int? Price { get; set; }
        public string Currency { get; set; }
        public int? Pw { get; set; }
        public int? Pcm { get; set; }
        public int? Pa { get; set; }
        public string PriceQualify { get; set; }
        public bool SaleRental { get; set; }
        public string? MarketStatus { get; set; }
        public string? BannerText { get; set; }

        public string PropertyType { get; set; }
        public bool CommercialListing { get; set; }
        public int? Bedrooms { get; set; }
        public int? Bathrooms { get; set; }
        public int? Reception { get; set; }
        public bool NewBuild { get; set; }
        public string? Image { get; set; }
        public bool VirtualTour{ get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTime.Now;
    }
}
