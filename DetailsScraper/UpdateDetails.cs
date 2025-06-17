using DatabaseContext;
using DatabaseContext.Models;
using HtmlAgilityPack;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Utilities;

namespace DetailsScraper
{
    public class UpdateDetails
    {
        public static ILog Logger { get; set; }
        public static List<UpdateListingDetails> Listings { get; set; }
        public static List<UpdateListingDetails> RetryListings { get; set; }
        public static bool IsRetry = false;
        static string urlRaw = "https://www.dacres.co.uk";
        public static void Run(int numberThreads, ILog logger, int retryTimes)
        {
            logger.Info("Updating the details scraper with " + numberThreads + " threads.");

            Logger = logger;
            using (AppDbContext context = new AppDbContext())
            {
                var newListings = context.UpdateListingDetails.ToList();
                Listings = newListings;
                RetryListings = new List<UpdateListingDetails>();
            }

            Task[] backgroundTasks = new Task[numberThreads];
            for (int i = 0; i < numberThreads; i++)
            {
                backgroundTasks[i] = Task.Run(() => StartScrape());
            }

            Task.WaitAll(backgroundTasks);

            for (int time = 0; time < retryTimes; time++)
            {
                logger.Info("Retry the failed Listing one more time");
                Listings = RetryListings;
                RetryListings = new List<UpdateListingDetails>();

                //if it's the last time we retry, then we mark the isRetry flag to be false
                if (time == retryTimes - 1)
                {
                    ScraperDetail.IsRetry = true;
                }

                backgroundTasks = new Task[numberThreads];
                for (int i = 0; i < numberThreads; i++)
                {
                    backgroundTasks[i] = Task.Run(() => StartScrape());
                }
                //wait for all threads to complete
                Task.WaitAll(backgroundTasks);
            }


            logger.Info("End Updating Scrape");
        }

        public static void StartScrape()
        {
            try
            {
                while (true)
                {
                    UpdateListingDetails listing;
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

        public static void PerformScrape(UpdateListingDetails listing)
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

                                string branchName = "UNKNOWN";
                                string branchUrl = "";
                                var office = jsonObject["office"];
                                if (office != null)
                                {
                                    var rawBranchName = office["name"]?.ToString() ?? "";
                                    rawBranchName = Regex.Replace(rawBranchName, "(?i)lettings", "");

                                    branchName = WebUtility.HtmlDecode(rawBranchName).Trim();
                                    branchUrl = BranchHelper.GetBranchUrl(rawBranchName);
                                }

                                using (AppDbContext context = new AppDbContext())
                                {
                                    var existingProperty = context.PropertyDetails
                                                                  .FirstOrDefault(p => p.ListingSiteRef == listing.ListingSiteRef);

                                    if (existingProperty != null && !string.Equals(existingProperty.BranchUrl, branchUrl, StringComparison.OrdinalIgnoreCase) || existingProperty != null && !string.Equals(existingProperty.BranchName, branchName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        existingProperty.BranchName = branchName;
                                        existingProperty.BranchUrl = branchUrl;
                                        context.SaveChanges();
                                    }

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
    }
}
