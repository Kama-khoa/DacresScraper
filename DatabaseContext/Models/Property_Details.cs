using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DatabaseContext.Models
{
    public class PropertyDetails
    {
        public int Id { get; set; }
        public string Postcode { get; set; }
        public string? AddedDate { get; set; }
        public int ListingSiteRef { get; set; } 
        public string? PriceQualify { get; set; }
        public string Description { get; set; }

        public int? Bedrooms { get; set; }
        public int? Bathrooms { get; set; }
        public int? Receptions { get; set; }

        public string BranchName { get; set; }
        public string BranchUrl { get; set; }
        public string? FullBrochure { get; set; }

        public double? Longitude { get; set; }
        public double? Latitude { get; set; } 

        public string Tenure { get; set; }           
        public string? KeyFeatures { get; set; }
        public string Images { get; set; }
        public string? EPC { get; set; }
        public string? FloorPlan { get; set; }
        public string? VirtualTour { get; set; }
        public string? RunSession { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
