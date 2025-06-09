using DatabaseContext;
using HtmlAgilityPack;
using DatabaseContext.Models;
using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using Utilities;
using log4net;

namespace RealEstateScraper
{
    public class ScraperService
    {
        public static ILog Logger { get; set; } 
        public static List<BasicListingUrl> RetryPages { get; set; }
        public static bool IsRetry = false;
        private static string urlRaw = "https://www.dacres.co.uk";

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

        public static string BuildListUrl(ListingTypes listingTypes, PropertyTypes propertyTypes, int? page)
        {
            string listingType = "";
            string propertyType = "";

            switch (listingTypes)
            {
                case ListingTypes.Sale:
                    listingType = "sales";
                    break;
                case ListingTypes.Rent:
                    listingType = "lettings";
                    break;
                default:
                    throw new Exception("The Listing type is invalid");
            }

            switch (propertyTypes)
            {
                case PropertyTypes.Commercial:
                    propertyType = "tag-commercial";
                    break;
                case PropertyTypes.Residential:
                    propertyType = "tag-residential";
                    break;
                case PropertyTypes.NewHomes:
                    propertyType = "tag-new-home";
                    break;
                case PropertyTypes.Agriculture:
                    propertyType = "tag-agricultural-properties";
                    break;
                default:
                    throw new Exception("The Property type is invalid");
            }

            //return $"{urlRaw}/properties/{listingType}/{propertyType}/page-{page}";

            if (page > 0)
            {
                return $"{urlRaw}/properties/{listingType}/{propertyType}/page-{page}";
            }
            return $"{urlRaw}/properties/{listingType}/{propertyType}";
        }

        public static int GetTotalPages(ListingTypes listingTypes, PropertyTypes propertyTypes)
        {
            using (var webClient = new WebClient())
            {
                webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                string firstPageUrl = BuildListUrl(listingTypes, propertyTypes, null);
                string html = webClient.DownloadString(firstPageUrl);

                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(html);

                int resultsPerPage = 14;
                int totalPages = 1;

                var countSpan = doc.DocumentNode.SelectSingleNode("//span[@class='count']");
                if (countSpan != null && int.TryParse(countSpan.InnerText.Trim(), out int totalResults))
                {
                    totalPages = (int)Math.Ceiling(totalResults / (double)resultsPerPage);
                }

                Logger.Info($"Total Results: {totalPages * resultsPerPage} ({totalPages} pages)");
                return totalPages;
            }
        }

        public static void Startcrawler(ListingTypes listingTypes, PropertyTypes propertyTypes, ConcurrentQueue<int> pageQueue)
        {
            using(var webClient = new WebClient())
            {
                webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36 OPR/119.0.0.0");

                while (pageQueue.TryDequeue(out int page))
                {
                    try
                    {
                        var url = BuildListUrl(listingTypes, propertyTypes, page);
                        Logger.Info("Scraping page number: " + page);
                        PerformScrape(listingTypes, propertyTypes, url, webClient);
                        Logger.Info($"Page {page} scraped successfully.");
                    } 
                    catch (Exception e)
                    {
                        Logger.Error($"Error scraping page {page}: {e.Message}");
                    }
                }
            }
        }

        public static void PerformScrape(ListingTypes listingTypes, PropertyTypes propertyTypes, string url, WebClient webClient)
        {
            try
            {
                AppConfig appConfig = new AppConfig();

                webClient.Proxy = new WebProxy(appConfig.ProxyUrl)
                {
                    Credentials = new NetworkCredential(appConfig.ProxyUsername, appConfig.ProxyPassword)
                };

                var html = webClient.DownloadString(url);

                HtmlDocument htmldocument = new HtmlDocument();

                htmldocument.LoadHtml(html);

                var divs = htmldocument.DocumentNode.SelectNodes("//div[@class='block-content']");

                int i = 0;
                foreach (var div in divs)
                {
                    if (!int.TryParse(div.GetAttributeValue("data-id", ""), out int id)) continue;
                    var listingUrl = div.Descendants("a")
                                        .FirstOrDefault(a => a.GetAttributeValue("class", "").Contains("property_link"))
                                        ?.GetAttributeValue("data-base-url", "");
                    var fullUrl = $"{urlRaw}{listingUrl}";
                    var address = div.Descendants("h3").FirstOrDefault()?.InnerText.Trim();

                    var postcode = "";
                    var postcodeNode = Regex.Matches(address, @"[A-Z]{1,2}[0-9]{1,2}");
                    if (postcodeNode.Count > 0)
                    {
                        postcode = postcodeNode[0].Value;
                    }

                    bool saleRental = listingTypes == ListingTypes.Rent;

                    var priceSpan = div.Descendants("span")
                                        .FirstOrDefault(span => span.GetAttributeValue("class", "").Contains("price"));
                    string priceText = priceSpan?.InnerText.Trim() ?? "";
                    priceText = Regex.Replace(priceText, @"\s+", " ");
                    priceText = priceText.Replace(",", "").Trim();

                    string priceQuantify = "";
                    int? price = null, pcm = null, pw = null, pa = null;
                    if (!string.IsNullOrEmpty(priceText))
                    {
                        var digitsOnly = new string(priceText.Where(char.IsDigit).ToArray());
                        if (saleRental)
                        {
                            if (priceText.Contains("POA", StringComparison.OrdinalIgnoreCase) || priceText.Contains("Price on application", StringComparison.OrdinalIgnoreCase))
                            {
                                pcm = -1;
                                priceQuantify = "POA";
                            }
                            if (priceText.Contains("pa", StringComparison.OrdinalIgnoreCase))
                            {
                                pa = int.TryParse(digitsOnly, out var parsedValue) ? parsedValue : null;
                                priceQuantify = "Price Annum";
                            }
                            else if (priceText.Contains("pcm", StringComparison.OrdinalIgnoreCase))
                            {
                                pcm = int.TryParse(digitsOnly, out var parsedValue) ? parsedValue : null;
                                priceQuantify = "Per Calendar Month";
                            }
                            else if (priceText.Contains("pw", StringComparison.OrdinalIgnoreCase))
                            {
                                pw = int.TryParse(digitsOnly, out var parsedValue) ? parsedValue : null;
                                priceQuantify = "Per Week";
                            }
                        }
                        else
                        {
                            price = int.TryParse(digitsOnly, out var parsedValue) ? parsedValue : null;

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

                    string currencySymbol = new string(priceText.Where(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)).ToArray());
                    string currencyCode = CurrencyHelper.CurrencySymbolToIsoCode(currencySymbol);

                    bool commercialListing = propertyTypes == PropertyTypes.Commercial;

                    bool newBuild = propertyTypes == PropertyTypes.NewHomes;

                    var marketStatusImg = div.Descendants("img")
                                             .FirstOrDefault(img => img.GetAttributeValue("class", "").Equals("property-status", StringComparison.OrdinalIgnoreCase));

                    string marketStatus = "Available"; // Default value

                    if (marketStatusImg != null)
                    {
                        var src = marketStatusImg.GetAttributeValue("src", "").ToLower();

                        if (src.Contains("let_agreed_new"))
                        {
                            marketStatus = "Let Agreed";
                        }
                        else if (src.Contains("sstc_new"))
                        {
                            marketStatus = "Sold STC";
                        }
                    }

                    bool virtualTour = false;
                    var virtualTourImg = div.Descendants("img")
                                            .FirstOrDefault(img => img.GetAttributeValue("alt", "").Equals("Virtual tour", StringComparison.OrdinalIgnoreCase));
                    if (virtualTourImg != null) { virtualTour = true; }

                    var bannerImg = div.Descendants("img")
                                        .FirstOrDefault(img => img.GetAttributeValue("class", "").Equals("property-status", StringComparison.OrdinalIgnoreCase));

                    string bannerText = "";

                    if (bannerImg != null)
                    {
                        var src = bannerImg.GetAttributeValue("src", "").ToLower();

                        if (src.Contains("let_agreed_new"))
                        {
                            bannerText = "Let Agreed";
                        }
                        else if (src.Contains("sstc_new"))
                        {
                            bannerText = "Sold STC";
                        }
                        else if (src.Contains("new-overlay"))
                        {
                            bannerText = "New";
                        }
                        if (virtualTour)
                        {
                            if (!string.IsNullOrEmpty(bannerText))
                            {
                                bannerText += " || Online Viewing";
                            }
                            else
                            {
                                bannerText = "Online Viewing";
                            }
                        }
                    }

                    var propertyTypeSpan = div.Descendants("span")
                                            .FirstOrDefault(span => span.GetAttributeValue("class", "").Contains("list-rooms"));

                    string fullText = propertyTypeSpan?.InnerText.Trim() ?? "";
                    string HouseType = "";

                    if (!string.IsNullOrEmpty(fullText))
                    {
                        int indexOfWith = fullText.IndexOf("with", StringComparison.OrdinalIgnoreCase);
                        if (indexOfWith > 0)
                        {
                            HouseType = fullText.Substring(0, indexOfWith).Trim();
                        }
                        else
                        {
                            HouseType = fullText.Trim();
                        }
                    }

                    var imageDiv = div.Descendants("img")
                                        .FirstOrDefault(img => img.GetAttributeValue("class", "").Equals("zoomable thumb", StringComparison.OrdinalIgnoreCase))
                                        ?.GetAttributeValue("src", "");
                    var image = "https:" + imageDiv;

                    var description = div.Descendants("p")
                                         .FirstOrDefault(p => p.GetAttributeValue("class", "").Contains("description"))
                                         ?.InnerText.Trim();
                    var bedrooms = div.Descendants("li")
                                        .FirstOrDefault(li => li.GetAttributeValue("class", "").Contains("beds"))
                                        ?.InnerText.Trim();
                    var bathrooms = div.Descendants("li")
                                        .FirstOrDefault(li => li.GetAttributeValue("class", "").Contains("baths"))
                                        ?.InnerText.Trim();
                    var receptions = div.Descendants("li")
                                        .FirstOrDefault(li => li.GetAttributeValue("class", "").Contains("reception"))
                                        ?.InnerText.Trim();

                    int? bedroomsInt = int.TryParse(bedrooms, out var b) ? b : 0;
                    int? bathroomsInt = int.TryParse(bathrooms, out var bath) ? bath : 0;
                    int? receptionsInt = int.TryParse(receptions, out var r) ? r : 0;

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
                        BannerText = bannerText,
                        PropertyType = HouseType,
                        PriceQualify = priceQuantify,
                        CommercialListing = commercialListing,
                        NewBuild = newBuild,
                        Bedrooms = bedroomsInt,
                        Bathrooms = bathroomsInt,
                        Reception = receptionsInt,
                        Image = image,
                        Pcm = pcm,
                        Pw = pw,
                        Pa = pa,
                        VirtualTour = virtualTour,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    using (var context = new AppDbContext())
                    {
                        context.Properties.Add(property);
                        context.SaveChanges();
                    }
                    i++;
                }
                Logger.Info($"Scraped {i} properties from {url}");
            }
            catch (Exception e)
            {
                Logger.Error($"Error scraping {url}");
                if (IsRetry)
                {
                    throw;
                }

                RetryPages.Add(new BasicListingUrl
                {
                    ListingUrl = url
                });
                Logger.Error($"Added {url} to retry list due to error");
                Logger.Error(e.Message);
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
                            PerformScrape(listingTypes, propertyTypes, listingPage.ListingUrl, webClient);
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
