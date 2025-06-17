using DatabaseContext;
using DatabaseContext.Models;
using HtmlAgilityPack;
using log4net;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
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
        private static int resultsPerPage = 12;
        private static int totalPages = 1;
        private static HttpClient httpClient;
        private static List<int> ListNewHomeIds = new List<int>();

        public static void Start(int numberOfThread, ILog logger)
        {
            Logger = logger;

            Logger.Info("Initializing HTTP client...");
            var handler = CookieSessionManager.GetHandlerWithCookies(out var cookies);
            httpClient = CookieSessionManager.CreateHttpClient(handler);

            Logger.Info("Starting the Url scraper...");

            Logger.Info("Scraping for sale properties Url:");
            Run(ListingTypes.Sale, numberOfThread);

            Logger.Info("Scraping for rent properties Url:");
            Run(ListingTypes.Rent, numberOfThread);

            Logger.Info("Scraping completed.");
        }

        public static void Run(ListingTypes listingTypes, int numberOfThread)
        {
            using (AppDbContext context = new AppDbContext())
            {
                RetryPages = new List<BasicListingUrl>();
            }

            Logger.Info("Starting scrape new homes");

            int newHomeTotalPages = GetTotalPages(listingTypes, true);

            var newHomePageQueue = new ConcurrentQueue<int>(Enumerable.Range(1, newHomeTotalPages));

            Task[] Tasks = new Task[numberOfThread];
            for (int i = 0; i < numberOfThread; i++)
            {
                var task = i;
                Tasks[task] = Task.Run(() => GetNewHome(listingTypes, newHomePageQueue));
            }

            Task.WaitAll(Tasks);

            Logger.Info("Starting scrape properties ...");

            int totalPages = GetTotalPages(listingTypes, false);

            var pageQueue = new ConcurrentQueue<int>(Enumerable.Range(1, totalPages));

            IsRetry = false;
            Task[] backgroundTasks = new Task[numberOfThread];
            for (int i = 0; i < numberOfThread; i++)
            {
                var task = i;
                backgroundTasks[task] = Task.Run(() => Startcrawler(listingTypes, pageQueue));
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
                backgroundTasks[task] = Task.Run(() => RetryScrape(listingTypes, numberOfThread));
            }
            //wait for all threads to complete
            Task.WaitAll(backgroundTasks);
        }

        #region build url
        public static string BuildUrl(ListingTypes listingTypes, int? page, bool newHome)
        {
            string channel = listingTypes == ListingTypes.Sale ? "residential-sales" : "residential-lettings";
            string fragment = "";
            string newhome = "";

            if (newHome)
            {
                newhome = "&_new_build=yes";
            }

            if (page.HasValue && page > 1)
            {
                fragment = $"/page/{page}";
            }

            return $"{urlRaw}/search{fragment}/?department={channel}{newhome}";
        }
        #endregion

        #region get total pages
        public static int GetTotalPages(ListingTypes listingTypes, bool newHome)
        {
            try
            {
                using (var webClient = new WebClient())
                {
                    webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36 OPR/119.0.0.0");

                    string firstPageUrl = BuildUrl(listingTypes, null, newHome);
                    string html = webClient.DownloadString(firstPageUrl);

                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    var countNode = doc.DocumentNode.SelectSingleNode("//h1[@class='page-info-title']");
                    if (countNode != null)
                    {
                        var match = Regex.Match(countNode.InnerText.Trim(), @"\d+");
                        if (match.Success && int.TryParse(match.Value, out int totalResults))
                        {
                            totalPages = (int)Math.Ceiling(totalResults / (double)resultsPerPage);
                        }
                        else
                        {
                            Logger.Info($"Results: 0 page");
                            return 0;
                        }    
                    }

                    Logger.Info($"Total Results: {totalPages * resultsPerPage} ({totalPages} pages)");
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

        public static void Startcrawler(ListingTypes listingTypes, ConcurrentQueue<int> pageQueue)
        {
                while (pageQueue.TryDequeue(out int page))
                {
                    try
                    {
                        string url = BuildUrl(listingTypes, page, false);
                        Logger.Info($"HTTP visiting: {url}");
                        PerformScrape(listingTypes, url);
                        Logger.Info($"Page {page} scraped successfully.");
                    }
                    catch (Exception e)
                    {
                        Logger.Error($"Error scraping page {page}: {e.Message}");
                    }
            }
        }

        private static void GetNewHome(ListingTypes listingTypes, ConcurrentQueue<int> pageQueue)
        {
            var newHomeIds = new ConcurrentBag<int>();

            while (pageQueue.TryDequeue(out int page))
            {
                try
                {
                    string url = BuildUrl(listingTypes, page, true);

                    Logger.Info($"HTTP visiting: {url}");

                    var html = httpClient.GetStringAsync(url).Result;

                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    var divs = doc.DocumentNode.SelectNodes("//div[contains(@class,'properties-block')]");
                    foreach (var div in divs)
                    {
                        try
                        {
                            var idElement = div.SelectSingleNode(".//a[@data-add-to-shortlist]");
                            var idStr = idElement.GetAttributeValue("data-add-to-shortlist", "").Trim();
                            if (int.TryParse(idStr, out int listingId))
                            {
                                newHomeIds.Add(listingId);
                            }
                        }
                        catch { continue; }
                    }

                    Logger.Info($"Page {page} scraped successfully.");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error at page {page}: {ex.Message}");
                }
            }

            lock (ListNewHomeIds)
            {
                ListNewHomeIds.AddRange(newHomeIds.Distinct());
            }
        }

        public static void PerformScrape(ListingTypes listingTypes, string url)
        {
            try
            {
                var html = httpClient.GetStringAsync(url).Result;

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var divs = doc.DocumentNode.SelectNodes("//div[contains(@class,'properties-block')]");

                if (divs == null)
                {
                    Logger.Warn($"No properties found in Url response for {url}");
                    return;
                }
                else
                {
                    int i = 0;
                    foreach (var div in divs)
                    {
                        try
                        {
                            var property = ParseProperty(div, listingTypes);
                            if (property != null)
                            {
                                if (ListNewHomeIds.Contains(property.ListingSiteRef))
                                {
                                    property.NewBuild = true;
                                }
                                using (var context = new AppDbContext())
                                {
                                    context.Properties.Add(property);
                                    context.SaveChanges();
                                }
                                i++;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Error parsing property: {ex.Message}");
                        }
                    }
                    Logger.Info($"Scraped {i} properties from Url: {url}");
                }
            }
            catch (Exception e)
            {
                Logger.Error($"Error scraping Url: {url}");
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

        private static Property ParseProperty(HtmlNode div, ListingTypes listingTypes)
        {
            try
            {
                int listingSiteRef; string listingUrl = null; string address = null; string postcodeDistrict = null; string marketStatus = null; string bannerText = null; string propertyType = null; 
                string currency = null; bool saleRental = false; string priceQualify = null; int? price = null, pcm = null, pw = null, pa = null; bool newBuild = false; string image = null;

                var idNode = div.SelectSingleNode(".//a[@data-add-to-shortlist]");
                listingSiteRef = int.TryParse(idNode?.GetAttributeValue("data-add-to-shortlist", "").Trim(), out var Value) ? Value : -1;

                var urlNode = div.SelectSingleNode(".//div[contains(@class, 'grid-box-card')]//a[@href]");
                listingUrl = urlNode?.GetAttributeValue("href", "")?.Trim();

                var addressNode = div.SelectSingleNode(".//div[contains(@class,'property-archive-title')]/h4");
                address = addressNode?.InnerText?.Trim();

                if (!string.IsNullOrEmpty(address))
                {
                    var postcodeMatch = Regex.Match(address, @"\b[A-Z]{1,2}\d{1,2}");
                    postcodeDistrict = postcodeMatch.Success ? postcodeMatch.Value : "";
                }

                var labelNode = div.SelectSingleNode(".//span[contains(@class,'property-label')]");
                bannerText = labelNode?.InnerText.Trim();

                marketStatus = bannerText;

                var typeNode = div.SelectSingleNode(".//p[@class='property-single-description']");
                propertyType = typeNode?.InnerText?.Trim();

                if (!string.IsNullOrEmpty(marketStatus) && propertyType.EndsWith(marketStatus, StringComparison.OrdinalIgnoreCase))
                {
                    propertyType = propertyType.Substring(0, propertyType.Length - marketStatus.Length).Trim();
                }

                propertyType = propertyType.Replace(",", " ||").Trim();

                var imgNode = div.SelectSingleNode(".//div[contains(@class, 'grid-img')]//img");
                image = imgNode?.GetAttributeValue("src", "")?.Trim();
                string imageUrl = !string.IsNullOrEmpty(image) ? (image.StartsWith("http") ? image : $"https:{image}") : null;

                saleRental = listingTypes == ListingTypes.Rent;

                var priceText = div.SelectSingleNode(".//h5[contains(@class, 'property-archive-price')]").InnerText.Trim();
                priceText = WebUtility.HtmlDecode(priceText);
                priceText = Regex.Replace(priceText, @"\s+", " ").Replace(",", "").Trim();

                string currencySymbol = new string(priceText.Where(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)).ToArray());
                string currencyCode = CurrencyHelper.CurrencySymbolToIsoCode(currencySymbol);

                if (!string.IsNullOrEmpty(priceText))
                {
                    var digitsOnly = new string(priceText.Where(char.IsDigit).ToArray());

                    if (saleRental)
                    {
                        if (priceText.Contains("POA", StringComparison.OrdinalIgnoreCase) || priceText.Contains("Price on application", StringComparison.OrdinalIgnoreCase))
                        {
                            pcm = -1;
                            priceQualify = "POA";
                        }
                        if (priceText.Contains("pa", StringComparison.OrdinalIgnoreCase))
                        {
                            pa = int.TryParse(digitsOnly, out var parsedValue) ? parsedValue : null;
                            priceQualify = "Price Annum";
                        }
                        else if (priceText.Contains("pcm", StringComparison.OrdinalIgnoreCase))
                        {
                            pcm = int.TryParse(digitsOnly, out var parsedValue) ? parsedValue : null;
                            priceQualify = "Per Calendar Month";
                        }
                        else if (priceText.Contains("pw", StringComparison.OrdinalIgnoreCase))
                        {
                            pw = int.TryParse(digitsOnly, out var parsedValue) ? parsedValue : null;
                            priceQualify = "Per Week";
                        }
                    }
                    else
                    {
                        price = int.TryParse(digitsOnly, out var parsedValue) ? parsedValue : null;

                        if (priceText.Contains("Guide Price", StringComparison.OrdinalIgnoreCase))
                        {
                            priceQualify = "Guide Price";
                        }
                        else if (priceText.Contains("Offers excess of", StringComparison.OrdinalIgnoreCase))
                        {
                            priceQualify = "Offers Excess";
                        }
                        else if (priceText.Contains("Offers region of", StringComparison.OrdinalIgnoreCase))
                        {
                            priceQualify = "Offers Region";
                        }
                        else if (priceText.Contains("Offers over", StringComparison.OrdinalIgnoreCase))
                        {
                            priceQualify = "Offers Over";
                        }
                    }
                }

                var property = new Property
                {
                    ListingSiteRef = listingSiteRef,
                    ListingUrl = listingUrl,
                    Address = address,
                    PostcodeDistrict = postcodeDistrict,
                    Price = price,
                    Currency = currencyCode,
                    SaleRental = saleRental,
                    MarketStatus = marketStatus,
                    BannerText = bannerText,
                    PropertyType = propertyType,
                    PriceQualify = priceQualify,
                    NewBuild = newBuild,
                    Image = imageUrl,
                    Pcm = pcm,
                    Pw = pw,
                    Pa = pa,
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

        public static void RetryScrape(ListingTypes listingTypes, int numberOfThread)
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
                        PerformScrape(listingTypes, listingPage.ListingUrl);              
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
