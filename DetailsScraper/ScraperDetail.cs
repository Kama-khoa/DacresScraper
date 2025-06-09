using Azure;
using DatabaseContext;
using DatabaseContext.Models;
using HtmlAgilityPack;
using log4net;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
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
                using (WebClient webClient = new WebClient())
                {
                    AppConfig appConfig = new AppConfig();

                    webClient.Proxy = new WebProxy(appConfig.ProxyUrl)
                    {
                        Credentials = new NetworkCredential(appConfig.ProxyUsername, appConfig.ProxyPassword)
                    };
                    webClient.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/69.0.3497.92 Safari/537.36";

                    html = webClient.DownloadString(listing.ListingUrl);
                    if (html == null)
                    {
                        return;
                    }
                }
                
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(html);

                var scriptNodes = doc.DocumentNode.SelectNodes("//script")
                                                  .FirstOrDefault(s => s.InnerText.Contains("google.maps.LatLng"));

                double? latitude = null;
                double? longitude = null;

                if (scriptNodes != null)
                {
                    var match = Regex.Match(scriptNodes.InnerText, @"google\.maps\.LatLng\s*\(\s*([\d\.-]+)\s*,\s*([\d\.-]+)\s*\)");
                    if (match.Success)
                    {
                        latitude = double.TryParse(match.Groups[1].Value, out var lat) ? lat : (double?)null;
                        longitude = double.TryParse(match.Groups[2].Value, out var lng) ? lng : (double?)null;
                    }
                }

                var detailsDiv = doc.DocumentNode.SelectSingleNode("//main");
                if (detailsDiv != null)
                {
                    var address = listing.Address.Trim();
                    var postcode = "";
                    var postcodeNode = Regex.Matches(address, @"[A-Z]{1,2}[0-9]{1,2}");
                    if (postcodeNode.Count > 0)
                    {
                        postcode = postcodeNode[0].Value;
                    }
                    var BranchNode = detailsDiv.Descendants("div")
                                            .FirstOrDefault(div => div.GetAttributeValue("class", "").Contains("branch-details-header"));
                    string branchName = BranchNode.Descendants("span")
                                                   .FirstOrDefault(span => span.GetAttributeValue("class", "").Contains("branch-name"))
                                                   ?.InnerText.Trim();
                    string branchPhone = BranchNode.Descendants("a")
                                                   .FirstOrDefault(a => a.GetAttributeValue("class", "").Contains("phone"))
                                                   ?.InnerText.Trim();
                    string branchLink = BranchNode.Descendants("a")
                                                   .FirstOrDefault(a => a.GetAttributeValue("class", "").Contains("btn sweep no-top"))
                                                   ?.GetAttributeValue("href", "");
                    string branchFullLink = $"{urlRaw}{branchLink}";
                    var fullBrochure = BranchNode.Descendants("a")
                                                 .FirstOrDefault(a => a.InnerText.Trim().Equals("Full brochure", StringComparison.OrdinalIgnoreCase))
                                                 ?.GetAttributeValue("href", "");
                    var descriptionNode = detailsDiv.Descendants("div")
                                            .FirstOrDefault(div => div.GetAttributeValue("class", "").Contains("property-description"));
                    string description = string.Join("\n", descriptionNode?.Descendants("p").Select(p => p.InnerText.Trim()));
                    var bedrooms = detailsDiv.Descendants("li")
                                             .FirstOrDefault(li => li.GetAttributeValue("class", "").Contains("beds"))
                                             ?.InnerText.Trim();
                    var bathrooms = detailsDiv.Descendants("li")
                                              .FirstOrDefault(li => li.GetAttributeValue("class", "").Contains("baths"))
                                              ?.InnerText.Trim();
                    var receptions = detailsDiv.Descendants("li")
                                               .FirstOrDefault(li => li.GetAttributeValue("class", "").Contains("reception"))
                                               ?.InnerText.Trim();
                    int? bedroomsInt = int.TryParse(bedrooms, out var b) ? b : (int?)null;
                    int? bathroomsInt = int.TryParse(bathrooms, out var bath) ? bath : (int?)null;
                    int? receptionsInt = int.TryParse(receptions, out var r) ? r : (int?)null;
                    var detailsTab = detailsDiv.Descendants("div")
                                               .FirstOrDefault(div => div.GetAttributeValue("id", "").Contains("tab-description"));
                    var keyFeatures = string.Join(" || ", detailsTab.Descendants("li").Select(li => li.InnerText.Trim()));
                    var tenure = "Unknown";
                    var tenureParagraph = detailsTab.Descendants("p")
                                       .FirstOrDefault(p => p.InnerHtml.Contains("Freehold", StringComparison.OrdinalIgnoreCase)
                                                         || p.InnerHtml.Contains("Leasehold", StringComparison.OrdinalIgnoreCase)
                                                         || p.InnerHtml.Contains("Commonhold", StringComparison.OrdinalIgnoreCase));
                    if (tenureParagraph != null && !tenureParagraph.InnerHtml.Equals("null"))
                    {
                        if (tenureParagraph.InnerHtml.Contains("Freehold", StringComparison.OrdinalIgnoreCase))
                        {
                            tenure = "Freehold";
                        }
                        else if (tenureParagraph.InnerHtml.Contains("Leasehold", StringComparison.OrdinalIgnoreCase))
                        {
                            tenure = "Leasehold";
                        }
                        else if (tenureParagraph.InnerHtml.Contains("Commonhold", StringComparison.OrdinalIgnoreCase))
                        {
                            tenure = "Commonhold";
                        }
                    }

                    var imagesTab = detailsDiv.Descendants("div")
                                                .FirstOrDefault(div => div.GetAttributeValue("id", "").Contains("tab-gallery"));
                    var images = "";
                    if (imagesTab != null && !imagesTab.InnerHtml.Equals("null", StringComparison.OrdinalIgnoreCase))
                    {
                        var imgSrcs = imagesTab.Descendants("img")
                              .Select(img => img.GetAttributeValue("src", ""))
                              .Select(src => src.StartsWith("http") ? src : "https:" + src);

                        images = string.Join(" || ", imgSrcs);
                    }

                    var floorPlanTab = detailsDiv.Descendants("div")
                                                .FirstOrDefault(div => div.GetAttributeValue("id", "").Contains("tab-floorplans"));
                    var floorPlan = "";
                    if (floorPlanTab != null && !floorPlanTab.InnerHtml.Equals("null", StringComparison.OrdinalIgnoreCase))
                    {
                        var imgSrcs = floorPlanTab.Descendants("img")
                              .Select(img => img.GetAttributeValue("src", ""))
                              .Select(src => src.StartsWith("http") ? src : "https:" + src);

                        floorPlan = string.Join(" || ", imgSrcs);
                    }

                    var epcTab = detailsDiv.Descendants("div")
                                                .FirstOrDefault(div => div.GetAttributeValue("id", "").Contains("tab-epcs"));
                    var epc = "";
                    if (epcTab != null && !epcTab.InnerHtml.Equals("null", StringComparison.OrdinalIgnoreCase))
                    {
                        var imgSrcs = epcTab.Descendants("img")
                              .Select(img => img.GetAttributeValue("src", "").Trim())
                              .Select(src => src.StartsWith("http") ? src : "https:" + src);

                        epc = string.Join(" || ", imgSrcs);
                    }

                    var areaGuideTab = detailsDiv.Descendants("div")
                                                .FirstOrDefault(div => div.GetAttributeValue("id", "").Contains("tab-area-guide"));
                    var areaGuide = "";
                    if (areaGuideTab != null && !areaGuideTab.InnerHtml.Equals("null"))
                    {
                        areaGuide = string.Join("\n", areaGuideTab?.Descendants("p").Select(p => p.InnerText.Trim()));
                    }

                    var virtualTour = "";
                    var virtualTourTab = detailsDiv.Descendants("div")
                                            .FirstOrDefault(div => div.GetAttributeValue("id", "").Contains("tab-virtual-tour"));
                    if (virtualTourTab != null && !virtualTourTab.InnerHtml.Equals("null"))
                    {
                        virtualTour = virtualTourTab.Descendants("iframe")
                                                    .FirstOrDefault(iframe => iframe.GetAttributeValue("src", "").Contains("youtube.com"))
                                                    ?.GetAttributeValue("src", "");
                    }

                    PropertyDetails propertyDetails = new PropertyDetails
                    {
                        Postcode = postcode,
                        ListingSiteRef = listing.ListingUrl,
                        Bedrooms = bedroomsInt,
                        Bathrooms = bathroomsInt,
                        Receptions = receptionsInt,
                        Description = description,
                        BranchName = branchName,
                        BranchPhone = branchPhone,
                        BranchLink = branchFullLink,
                        FullBrochure = fullBrochure,
                        Longitude = longitude,
                        Latitude = latitude,
                        Tenure = tenure,
                        KeyFeatures = keyFeatures,
                        Images = images,
                        FloorPlan = floorPlan,
                        EPC = epc,
                        AreaGuide = areaGuide,
                        VirtualTour = virtualTour,
                        CreatedAt = DateTime.Now
                    };
                    using (AppDbContext context = new AppDbContext())
                    {
                        context.PropertyDetails.Add(propertyDetails);
                        context.SaveChanges();
                    }
                        
                }
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
                    Logger.Info($"Error scraping details for {listing.ListingUrl}: {ex.Message}");
                }
            }
        }
    }
}