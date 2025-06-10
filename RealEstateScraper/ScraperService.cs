using DatabaseContext;
using DatabaseContext.Models;
using HtmlAgilityPack;
using log4net;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using Utilities;

namespace RealEstateScraper
{
    public class ScraperService
    {
        public static ILog Logger { get; set; } 
        public static List<BasicListingUrl> RetryPages { get; set; }
        public static bool IsRetry = false;
        private static string urlRaw = "https://www.dacres.co.uk";
        private static string apiBaseUrl = "https://www.dacres.co.uk/search.ljson";

        public static void Start(int numberOfThread, ILog logger)
        {
            Logger = logger;

            Logger.Info("Starting the Url scraper...");

            Logger.Info("Scraping for sale:");
            Logger.Info("Scraping residential properties Url:");
            Run(ListingTypes.Sale, PropertyTypes.Residential, numberOfThread);

            Logger.Info("Scraping commercial properties Url:");
            Run(ListingTypes.Sale, PropertyTypes.Commercial, numberOfThread);

            Logger.Info("Scraping new homes Url:");
            Run(ListingTypes.Sale, PropertyTypes.NewHomes, numberOfThread);

            Logger.Info("Scraping agricultural properties Url:");
            Run(ListingTypes.Sale, PropertyTypes.Agriculture, numberOfThread);

            Logger.Info("Scraping for rent:");
            Logger.Info("Scraping residential properties Url:");
            Run(ListingTypes.Rent, PropertyTypes.Residential, numberOfThread);

            Logger.Info("Scraping commercial properties Url:");
            Run(ListingTypes.Rent, PropertyTypes.Commercial, numberOfThread);

            Logger.Info("Scraping new homes Url:");
            Run(ListingTypes.Rent, PropertyTypes.NewHomes, numberOfThread);

            Logger.Info("Scraping agricultural properties Url:");
            Run(ListingTypes.Rent, PropertyTypes.Agriculture, numberOfThread);

            Logger.Info("Scraping completed.");
        }

        public static void Run(ListingTypes listingTypes, PropertyTypes propertyTypes, int numberOfThread)
        {
            using (AppDbContext context = new AppDbContext())
            {
                RetryPages = new List<BasicListingUrl>();
            }

            int totalPages = GetTotalPages(listingTypes, propertyTypes);

            var pageQueue = new ConcurrentQueue<int>(Enumerable.Range(1, totalPages));

            IsRetry = false;
            Task[] backgroundTasks = new Task[numberOfThread];
            for (int i = 0; i < numberOfThread; i++)
            {
                var task = i;
                backgroundTasks[task] = Task.Run(() => Startcrawler(listingTypes, propertyTypes, pageQueue));
            }

            //wait for all threads to complete
            Task.WaitAll(backgroundTasks);

            Logger.Info($"Started retry.");
            //retry to scrape the failed listing 1 more time
            IsRetry = true;

            backgroundTasks = new Task[numberOfThread];
            for (int i = 0; i < numberOfThread; i++)
            {
                var task = i;
                backgroundTasks[task] = Task.Run(() => RetryScrape(listingTypes, propertyTypes, numberOfThread));
            }
            //wait for all threads to complete
            Task.WaitAll(backgroundTasks);
        }

        #region build api url
        public static string BuildApiUrl(ListingTypes listingTypes, PropertyTypes propertyTypes, int? page)
        {
            string channel = listingTypes == ListingTypes.Sale ? "sales" : "lettings";
            string fragment = "";

            // Build fragment based on property type and page
            string propertyFragment = propertyTypes switch
            {
                PropertyTypes.Commercial => "tag-commercial",
                PropertyTypes.Residential => "tag-residential",
                PropertyTypes.NewHomes => "tag-new-home",
                PropertyTypes.Agriculture => "tag-agricultural-properties",
                _ => throw new Exception("The Property type is invalid")
            };

            if (page.HasValue && page > 1)
            {
                fragment = $"{propertyFragment}/page-{page}";
            }
            else
            {
                fragment = propertyFragment;
            }

            return $"{apiBaseUrl}?channel={channel}&fragment={fragment}";
        }
        #endregion

        #region get total pages
        public static int GetTotalPages(ListingTypes listingTypes, PropertyTypes propertyTypes)
        {
            try
            {
                using (var webClient = new WebClient())
                {
                    webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                    webClient.Headers.Add("Accept", "application/json");

                    string apiUrl = BuildApiUrl(listingTypes, propertyTypes, 1);
                    string jsonResponse = webClient.DownloadString(apiUrl);

                    var jsonObject = JObject.Parse(jsonResponse);

                    var pageObj = jsonObject["pagination"];
                    var totalResults = pageObj["total_count"]?.Value<int>() ?? 0;
                    var resultsPerPage = (pageObj["to_record"]?.Value<int>() - pageObj["from_record"]?.Value<int>()) + 1 ?? 14;

                    if (totalResults == 0)
                    {
                        var properties = jsonObject["properties"];
                        if (properties != null && properties.Type == JTokenType.Array)
                        {
                            resultsPerPage = properties.Count();
                            totalResults = resultsPerPage * 10; 
                        }
                    }

                    int totalPages = totalResults > 0 ? (int)Math.Ceiling(totalResults / (double)resultsPerPage) : 1;

                    Logger.Info($"Total Results: {totalResults} ({totalPages} pages)");
                    return totalPages;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting total pages: {ex.Message}");
                return 1; 
            }
        }
        #endregion

        public static void Startcrawler(ListingTypes listingTypes, PropertyTypes propertyTypes, ConcurrentQueue<int> pageQueue)
        {
            using (var webClient = new WebClient())
            {
                webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36 OPR/119.0.0.0");
                webClient.Headers.Add("Accept", "application/json");

                while (pageQueue.TryDequeue(out int page))
                {
                    try
                    {
                        var url = BuildApiUrl(listingTypes, propertyTypes, page);
                        Logger.Info("Scraping page number: " + page);
                        PerformApiScrape(listingTypes, propertyTypes, url, webClient);
                        Logger.Info($"Page {page} scraped successfully.");
                    }
                    catch (Exception e)
                    {
                        Logger.Error($"Error scraping page {page}: {e.Message}");
                    }
                }
            }
        }

        public static void PerformApiScrape(ListingTypes listingTypes, PropertyTypes propertyTypes, string apiUrl, WebClient webClient)
        {
            try
            {
                AppConfig appConfig = new AppConfig();

                webClient.Proxy = new WebProxy(appConfig.ProxyUrl)
                {
                    Credentials = new NetworkCredential(appConfig.ProxyUsername, appConfig.ProxyPassword)
                };

                var jsonResponse = webClient.DownloadString(apiUrl);
                var jsonObject = JObject.Parse(jsonResponse);

                var properties = jsonObject["properties"];
                if (properties == null || properties.Type != JTokenType.Array)
                {
                    Logger.Warn($"No properties found in API response for {apiUrl}");
                    return;
                }

                int processedCount = 0;
                foreach (var propertyJson in properties)
                {
                    try
                    {
                        var property = ParsePropertyFromJson(propertyJson, listingTypes, propertyTypes);
                        if (property != null)
                        {
                            using (var context = new AppDbContext())
                            {
                                context.Properties.Add(property);
                                context.SaveChanges();
                            }
                            processedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error parsing property from JSON: {ex.Message}");
                    }
                }

                Logger.Info($"Scraped {processedCount} properties from API: {apiUrl}");
            }
            catch (Exception e)
            {
                Logger.Error($"Error scraping API: {apiUrl}");
                if (IsRetry)
                {
                    throw;
                }

                RetryPages.Add(new BasicListingUrl
                {
                    ListingUrl = apiUrl
                });
                Logger.Error($"Added {apiUrl} to retry list due to error");
                Logger.Error(e.Message);
            }
        }

        private static Property ParsePropertyFromJson(JToken propertyJson, ListingTypes listingTypes, PropertyTypes propertyTypes)
        {
            try
            {
                var id = propertyJson["property_id"]?.Value<int>() ?? 0;
                var address = propertyJson["display_address"]?.Value<string>() ?? "";
                var listingUrl = propertyJson["property_url"]?.Value<string>() ?? "";
                var fullUrl = listingUrl.StartsWith("http") ? listingUrl : $"{urlRaw}{listingUrl}";

                var postcode = "";
                var postcodeMatches = Regex.Matches(address, @"[A-Z]{1,2}[0-9]{1,2}");
                if (postcodeMatches.Count > 0)
                {
                    postcode = postcodeMatches[0].Value;
                }

                bool saleRental = listingTypes == ListingTypes.Rent;

                var priceText = propertyJson["price_with_price_text"]?.Value<string>() ?? "";
                priceText = Regex.Replace(priceText, @"\s+", " ");
                priceText = priceText.Replace(",", "").Trim();

                string priceQuantify = "";
                int? price = null, pcm = null, pw = null, pa = null;

                string currencySymbol = new string(priceText.Where(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)).ToArray());
                string currencyCode = CurrencyHelper.CurrencySymbolToIsoCode(currencySymbol);

                if (!string.IsNullOrEmpty(priceText))
                {
                    if (saleRental)
                    {
                        if (priceText.Contains("POA", StringComparison.OrdinalIgnoreCase) || priceText.Contains("Price on application", StringComparison.OrdinalIgnoreCase))
                        {
                            pcm = -1;
                            priceQuantify = "POA";
                        }
                        if (priceText.Contains("pa", StringComparison.OrdinalIgnoreCase))
                        {
                            pa = propertyJson["price_value"]?.Value<int?>();
                            priceQuantify = "Price Annum";
                        }
                        else if (priceText.Contains("pcm", StringComparison.OrdinalIgnoreCase))
                        {
                            pcm = propertyJson["price_value"]?.Value<int?>();
                            priceQuantify = "Per Calendar Month";
                        }
                        else if (priceText.Contains("pw", StringComparison.OrdinalIgnoreCase))
                        {
                            pw = propertyJson["price_value"]?.Value<int?>();
                            priceQuantify = "Per Week";
                        }
                    }
                    else
                    {
                        price = propertyJson["price_value"]?.Value<int?>();

                        if (priceText.Contains("Guide Price", StringComparison.OrdinalIgnoreCase))
                        {
                            priceQuantify = "Guide Price";
                        }
                        else if (priceText.Contains("Offers excess of", StringComparison.OrdinalIgnoreCase))
                        {
                            priceQuantify = "Offers Excess";
                        }
                        else if (priceText.Contains("Offers region of", StringComparison.OrdinalIgnoreCase))
                        {
                            priceQuantify = "Offers Region";
                        }
                        else if (priceText.Contains("Offers over", StringComparison.OrdinalIgnoreCase))
                        {
                            priceQuantify = "Offers Over";
                        }
                    }
                }

                var propertyType = propertyJson["property_type"]?.Value<string>() ?? "";
                var bedrooms = propertyJson["bedrooms"]?.Value<int?>() ?? 0;
                var bathrooms = propertyJson["bathrooms"]?.Value<int?>() ?? 0;
                var receptions = propertyJson["reception_rooms"]?.Value<int?>() ?? 0;

                var marketStatus = propertyJson["status"]?.Value<string>() ?? null;
                var virtualTour = propertyJson["has_virtual_tour?"]?.Value<bool>() ?? false;
                string? bannerText = propertyJson["photo_overlay"]?.Value<string>().Trim() ?? null;
                string? banner = null;
                string? bannerSrc = null;

                if (!string.IsNullOrEmpty(bannerText))
                {
                    var match = Regex.Match(bannerText, "src=['\"](.*?)['\"]");
                    if (match.Success)
                    {
                        bannerSrc = match.Groups[1].Value;
                    }
                    if (bannerSrc.Contains("let_agreed_new", StringComparison.OrdinalIgnoreCase))
                    {
                        banner = "Let Agreed";
                    }
                    else if (bannerSrc.Contains("sstc_new", StringComparison.OrdinalIgnoreCase))
                    {
                        banner = "Sold STC";
                    }
                    else if (bannerSrc.Contains("new-overlay", StringComparison.OrdinalIgnoreCase))
                    {
                        banner = "New";
                    }
                }
                if (virtualTour)
                {
                    if (!string.IsNullOrEmpty(banner))
                    {
                        banner += " || Online Viewing";
                    }
                    else
                    {
                        banner = "Online Viewing";
                    }
                }

                var imageUrl = "";
                var images = propertyJson["photo"].Value<string>() ?? "";
                if (images != null && images.Any())
                {
                    imageUrl = images.StartsWith("http") ? images : $"https:{images}";
                }

                bool commercialListing = propertyTypes == PropertyTypes.Commercial;
                bool newBuild = propertyTypes == PropertyTypes.NewHomes;

                var property = new Property
                {
                    ListingSiteRef = id,
                    ListingUrl = fullUrl,
                    Address = address,
                    PostcodeDistrict = postcode,
                    Price = price,
                    Currency = currencyCode,
                    SaleRental = saleRental,
                    MarketStatus = marketStatus,
                    BannerText = banner,
                    PropertyType = propertyType,
                    PriceQualify = priceQuantify,
                    CommercialListing = commercialListing,
                    NewBuild = newBuild,
                    Bedrooms = bedrooms,
                    Bathrooms = bathrooms,
                    Reception = receptions,
                    Image = imageUrl,
                    Pcm = pcm,
                    Pw = pw,
                    Pa = pa,
                    VirtualTour = virtualTour,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                return property;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error parsing property JSON: {ex.Message}");
                return null;
            }
        }


        public static void RetryScrape(ListingTypes listingTypes, PropertyTypes propertyTypes, int numberOfThread)
        {
            BasicListingUrl listingPage;
            try
            { 
                while (true)
                {
                    lock (RetryPages)
                    {
                        listingPage = RetryPages.FirstOrDefault();
                        if (listingPage == null)
                        {
                            break;
                        }
                        
                        RetryPages.Remove(listingPage);
                    }

                    try
                    {
                        using (var webClient = new WebClient())
                        {
                            PerformApiScrape(listingTypes, propertyTypes, listingPage.ListingUrl, webClient);
                        }
                    
                    }
                    catch (Exception e)
                    {
                        {
                            if (IsRetry)
                            {
                                using (AppDbContext context = new AppDbContext())
                                {
                                    FailedItem failedItem = new FailedItem()
                                    {
                                        CreatedDate = DateTime.UtcNow,
                                        IsItemDeleted = false,
                                        ItemType = 0,
                                        Reference = listingPage.ListingSiteRef,
                                        Url = listingPage.ListingUrl,
                                        ErrorMessage = e.Message
                                    };
                                    context.FailedItems.Add(failedItem);
                                    context.SaveChanges();
                                }
                            }
                            //otherwise add the listing to retry list to scrape again
                            else
                            {
                                RetryPages.Add(listingPage);
                            }
                            Logger.Error($"Retry scraping page {listingPage.ListingUrl} error due to: {e.Message}");
                        }
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
                        ItemType = -1,
                        ErrorMessage = ex.Message
                    };
                    context.FailedItems.Add(failedItem);
                    context.SaveChanges();
                }
                Logger.Error($"Error in RetryScrape: {ex.Message}");
            }
        }
    }
}
