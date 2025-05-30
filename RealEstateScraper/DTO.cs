using System;

namespace RealEstateScraper.Dtos
{
    public class Property
    {
        public int Id { get; set; }
        public int PropertyId { get; set; }
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
        public string MarketStatus { get; set; }
        public string? BannerText { get; set; }
        public string PropertyType { get; set; }
        public bool CommercialListing { get; set; }
        public int? Bedrooms { get; set; }
        public int? Bathrooms { get; set; }
        public int? Reception { get; set; }
        public bool NewBuild { get; set; }
        public string? Image { get; set; }
        public bool VirtualTour { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class PropertyDetailsDto
    {
        public int Id { get; set; }
        public string Postcode { get; set; }
        public string? BannerText { get; set; }
        public string ListingSiteRef { get; set; }
        public string Description { get; set; }

        public int? Bedrooms { get; set; }
        public int? Bathrooms { get; set; }
        public int? Receptions { get; set; }

        public string BranchName { get; set; }
        public string BranchPhone { get; set; }
        public string BranchLink { get; set; }
        public string? FullBrochure { get; set; }

        public double? Longitude { get; set; }
        public double? Latitude { get; set; }

        public string Tenure { get; set; }
        public string KeyFeatures { get; set; }
        public string Images { get; set; }
        public string? EPC { get; set; }
        public string? FloorPlan { get; set; }
        public string? AreaGuide { get; set; }
        public string? VirtualTour { get; set; }

        public string? RunSession { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}