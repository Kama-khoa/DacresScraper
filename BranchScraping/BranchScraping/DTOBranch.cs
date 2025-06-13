using System.Collections.Generic;
using Newtonsoft.Json;

namespace BranchScraping.DTOs
{
    public class BranchDto
    {
        [JsonProperty("lat")]
        public double Lat { get; set; }

        [JsonProperty("lng")]
        public double Lng { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("branch_url")]
        public string BranchUrl { get; set; }

        [JsonProperty("agency_name")]
        public string AgencyName { get; set; }

        [JsonProperty("branch_id")]
        public int BranchId { get; set; }

        [JsonProperty("branch_logo")]
        public string BranchLogo { get; set; }

        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("sales_enabled")]
        public bool SalesEnabled { get; set; }

        [JsonProperty("lettings_enabled")]
        public bool LettingsEnabled { get; set; }

        [JsonProperty("franchisee_name")]
        public string FranchiseeName { get; set; }

        [JsonProperty("photo")]
        public string Photo { get; set; }

        [JsonProperty("vox_numbers")]
        public object VoxNumbers { get; set; }
    }

    public class BranchResponse
    {
        [JsonProperty("branches")]
        public List<BranchDto> Branches { get; set; } = new List<BranchDto>();
    }
}