using Azure;
using DatabaseContext;
using DatabaseContext.Models;
using HtmlAgilityPack;
using log4net;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Utilities;

namespace DetailsScraper
{
    public class ScraperDetail
    {
        public static ILog Logger { get; set; }
        public static List<BasicListingUrl> Listings { get; set; }
        public static List<BasicListingUrl> RetryListings { get; set; }
        public static bool IsRetry = false;
        static string urlRaw = "https://www.dacres.co.uk";
        public static void StartScrape()
        {
            try
            {
                while (true)
                {
                    BasicListingUrl listing;
                    lock (Listings)
                    {
                        listing = Listings.FirstOrDefault();

                        //if there's no listing requires update - exit
                        if (listing == null)
                        {
                            break;
                        }
                        //otherwise, start scrape for this listing
                        //Mark the listing is dirty so that the other threads will not pick it up
                        Listings.Remove(listing);
                    }
                    try
                    {
                        //scrape the data 
                        PerformScrape(listing);
                    }
                    catch (Exception e)
                    {
                        // If it's the last retry scrape, log the failed listing to db
                        if (IsRetry)
                        {
                            using (AppDbContext context = new AppDbContext())
                            {
                                FailedItem failedItem = new FailedItem()
                                {
                                    CreatedDate = DateTime.UtcNow,
                                    IsItemDeleted = false,
                                    ItemType = 1,
                                    Url = listing.ListingUrl,
                                    Reference = listing.ListingSiteRef,
                                    ErrorMessage = e.Message
                                };
                                context.FailedItems.Add(failedItem);
                                context.SaveChanges();
                            }
                        }
                        //otherwise add the listing to retry list to scrape again
                        else
                        {
                            RetryListings.Add(listing);
                        }

                        Logger.Error(("Scrape listing {0} error due to: {1}", listing.ListingSiteRef, e.Message));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("An error occurred during scraping: " + ex.Message);
            }
        }

        public static void PerformScrape(BasicListingUrl listing)
        {
            Logger.Info($"Scraping details for {listing.ListingUrl}");
            try
            {
                string html = string.Empty;

                CookieContainer cookies;
                var handler = CookieSessionManager.GetHandlerWithCookies(out cookies);
                using (HttpClient client = CookieSessionManager.CreateHttpClient(handler))
                {
                    HttpResponseMessage response = client.GetAsync(listing.ListingUrl).Result;
                    if (!response.IsSuccessStatusCode)
                        return;

                    html = response.Content.ReadAsStringAsync().Result;
                    if (string.IsNullOrWhiteSpace(html))
                        return;

                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    var jsonLinkNode = doc.DocumentNode
                                              .SelectNodes("//link")
                                              ?.FirstOrDefault(link =>
                                                  link.GetAttributeValue("rel", "").Equals("alternate", StringComparison.OrdinalIgnoreCase) &&
                                                  link.GetAttributeValue("type", "").Equals("application/json", StringComparison.OrdinalIgnoreCase));

                    if (jsonLinkNode != null)
                    {
                        string jsonUrl = jsonLinkNode.GetAttributeValue("href", "");
                        if (!string.IsNullOrEmpty(jsonUrl))
                        {
                            var jsonResponse = client.GetAsync(jsonUrl).Result;
                            if (jsonResponse.IsSuccessStatusCode)
                            {
                                var jsonContent = jsonResponse.Content.ReadAsStringAsync().Result;

                                JObject jsonObject = JsonConvert.DeserializeObject<JObject>(jsonContent);

                                var postcode = jsonObject["address_postcode"]?.ToString()?.Trim() ?? string.Empty;
                                var addedDate = jsonObject["date"]?.ToString()?.Trim() ?? string.Empty;
                                var description = StripHtmlTags(jsonObject["description"]?.ToString() ?? "");
                                var priceQualify = jsonObject["price_qualifier"]?.ToString()?.Trim() ?? string.Empty;
                                if (string.IsNullOrEmpty(priceQualify) || priceQualify.Equals("null", StringComparison.OrdinalIgnoreCase))
                                {
                                    var priceText = jsonObject["price_formatted"]?.ToString()?.Trim() ?? string.Empty;
                                    if (priceText.Contains("POA", StringComparison.OrdinalIgnoreCase) || priceText.Contains("Price on application", StringComparison.OrdinalIgnoreCase))
                                    {
                                        priceQualify = "POA";
                                    }
                                    if (priceText.Contains("pa", StringComparison.OrdinalIgnoreCase))
                                    {
                                        priceQualify = "Price Annum";
                                    }
                                    else if (priceText.Contains("pcm", StringComparison.OrdinalIgnoreCase))
                                    {
                                        priceQualify = "Per Calendar Month";
                                    }
                                    else if (priceText.Contains("pw", StringComparison.OrdinalIgnoreCase))
                                    {
                                        priceQualify = "Per Week";
                                    }

                                    priceQualify = "Unknown";
                                }

                                var bedrooms = jsonObject["bedrooms"]?.ToString()?.Trim();
                                var bathrooms = jsonObject["bathrooms"]?.ToString()?.Trim();
                                var receptions = jsonObject["reception_rooms"]?.ToString()?.Trim();

                                int? bedroomsInt = int.TryParse(bedrooms, out var b) ? b : (int?)null;
                                int? bathroomsInt = int.TryParse(bathrooms, out var bath) ? bath : (int?)null;
                                int? receptionsInt = int.TryParse(receptions, out var r) ? r : (int?)null;

                                var longitude = jsonObject["longitude"]?.ToString()?.Trim();
                                var latitude = jsonObject["latitude"]?.ToString()?.Trim();

                                double? longitudeDouble = double.TryParse(longitude, out var lon) ? lon : (double?)null;
                                double? latitudeDouble = double.TryParse(latitude, out var lat) ? lat : (double?)null;

                                var tenure = jsonObject["tenure"]?.ToString()?.Trim() ?? "Unknown";

                                string? fullBrochure = null;
                                var brochures = jsonObject["brochures"];
                                if (brochures != null && brochures.HasValues)
                                {
                                    fullBrochure = brochures.First()?["url"]?.ToString();
                                }
                                string? floorPlan = null;
                                var floorplans = jsonObject["floorplans"];
                                if (floorplans != null && floorplans.HasValues)
                                {
                                    floorPlan = floorplans.First()?["url"]?.ToString();
                                }
                                string? epc = null;
                                var epcs = jsonObject["epcs"];
                                if (epcs != null && epcs.HasValues)
                                {
                                    epc = epcs.First()?["url"]?.ToString();
                                }
                                string? virtualTourUrl = null;
                                var virtualTour = jsonObject["virtual_tours"];
                                if (virtualTour != null && virtualTour.HasValues)
                                {
                                    virtualTourUrl = virtualTour.FirstOrDefault()?["url"]?.ToString();
                                }

                                var imageAllUrls = "";
                                var images = jsonObject["images"];
                                if (images != null && images.HasValues)
                                {
                                    var imageUrls = images.Select(img => img["url"]?.ToString()).Where(url => !string.IsNullOrEmpty(url)).ToList();
                                    imageAllUrls = string.Join("||", imageUrls);
                                }

                                string branchName = "UNKNOWN";
                                var office = jsonObject["office"];
                                if (office != null)
                                {
                                    branchName = office["name"]?.ToString() ?? "";
                                }
                                var keyFeatures = "";
                                var features = jsonObject["features"];
                                if (features != null && features.HasValues)
                                {
                                    var featuresList = features.Select(f => f?.ToString()?.TrimStart('•', ' '))
                                                               .Where(f => !string.IsNullOrEmpty(f))
                                                               .ToList();
                                    keyFeatures = string.Join("||", featuresList);
                                }
                                                                
                                PropertyDetails propertyDetails = new PropertyDetails
                                {
                                    ListingSiteRef = listing.ListingSiteRef,
                                    Postcode = postcode,
                                    AddedDate = addedDate,
                                    PriceQualify = priceQualify,
                                    Bedrooms = bedroomsInt,
                                    Bathrooms = bathroomsInt,
                                    Receptions = receptionsInt,
                                    Description = description,
                                    BranchName = branchName,
                                    FullBrochure = fullBrochure,
                                    Longitude = longitudeDouble,
                                    Latitude = latitudeDouble,
                                    Tenure = tenure,
                                    KeyFeatures = keyFeatures,
                                    Images = imageAllUrls,
                                    FloorPlan = floorPlan,
                                    EPC = epc,
                                    VirtualTour = virtualTourUrl,
                                    RunSession = GenerateRunSession()
                                };
                                using (AppDbContext context = new AppDbContext())
                                {
                                    context.PropertyDetails.Add(propertyDetails);
                                    context.SaveChanges();
                                }
                            }
                        }
                    }
                }
                Logger.Info($"Finished scrape details for {listing.ListingSiteRef}");
            }
            catch (Exception ex)
            {
                using (AppDbContext context = new AppDbContext())
                {
                    FailedItem failedItem = new FailedItem()
                    {
                        CreatedDate = DateTime.UtcNow,
                        IsItemDeleted = false,
                        ItemType = 1,
                        Url = listing.ListingUrl,
                        Reference = listing.ListingSiteRef,
                        ErrorMessage = ex.Message
                    };
                    context.FailedItems.Add(failedItem);
                    context.SaveChanges();
                }

                Logger.Info($"Error scraping details for {listing.ListingUrl}: {ex.Message}");
            }
        }
        private static string StripHtmlTags(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Simple HTML tag removal - you might want to use a more robust method
            return System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", string.Empty)
                                                        .Replace("&pound;", "£")
                                                        .Replace("&amp;", "&")
                                                        .Replace("&#8211;", "–")
                                                        .Trim();
        }
        private static string GenerateRunSession()
        {
            var now = DateTime.Now;
            string timeOfDay = now.Hour < 12 ? "Morning" : "Afternoon";
            return timeOfDay;
        }
    }
}