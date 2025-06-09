using Azure;
using DatabaseContext;
using DatabaseContext.Models;
using HtmlAgilityPack;
using log4net;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BranchScraping
{
    public class ScrapeBranch
    {
        public static List<NewBranchUrl> Branches { get; set; }
        public static List<NewBranchUrl> RetryBranches { get; set; }
        public static bool IsRetry = false;
        public static ILog Logger { get; set; }

        public static void StartScrape()
        {
            try
            {
                while (true)
                {
                    NewBranchUrl branch;
                    lock (Branches)
                    {
                        branch = Branches.FirstOrDefault();

                        //if there's no listing requires update - exit
                        if (branch == null)
                        {
                            break;
                        }
                        //otherwise, start scrape for this listing
                        //Remove the details so that the other threads will not pick it up
                        Branches.Remove(branch);

                    }

                    try
                    {
                        //scrape the data 
                        PerformScrape(branch);
                    }
                    catch (Exception e)
                    {
                        // If it's the last retry scrape, log the failed branch to db
                        if (IsRetry)
                        {
                            using (AppDbContext context = new AppDbContext())
                            {
                                FailedItem failedItem = new FailedItem()
                                {
                                    CreatedDate = DateTime.UtcNow,
                                    IsItemDeleted = false,
                                    ItemType = 2,
                                    Url = branch.BranchUrl,
                                    ErrorMessage = e.Message
                                };
                                context.FailedItems.Add(failedItem);
                                context.SaveChanges();
                            }
                        }
                        //otherwise add the branch to retry list to scrape again
                        else
                        {
                            RetryBranches.Add(branch);
                        }

                        Logger.Error(("Scrape branch {0} error due to: {1}", branch.BranchName, e.Message));
                    }

                }
            }
            catch (Exception ex)
            {
                Logger.Error("An error occurred during scraping: " + ex.Message);
            }
        }

        public static void PerformScrape(NewBranchUrl branch)
        {
            Logger.Info($"Scraping details for {branch.BranchUrl}");
            try
            {
                string page = string.Empty;
                using (WebClient webClient = new WebClient())
                {
                    //webClient.Proxy = new WebProxy("http://brd.superproxy.io:22225")
                    //{
                    //    Credentials = new NetworkCredential(
                    //        "lum-customer-c_9b0c3167-zone-data_center",
                    //        "r80ykn3xmrfp"
                    //    )
                    //};
                    webClient.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/69.0.3497.92 Safari/537.36";

                    page = webClient.DownloadString(branch.BranchUrl);
                    if (page == null)
                    {
                        return;
                    }
                }
                HtmlDocument htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(page);

                var html = htmlDocument.DocumentNode.Descendants("body").First();
                if(html != null)
                {
                    var branchName = html.Descendants("h1")
                                         .FirstOrDefault(h1 => h1.GetAttributeValue("class", "").Contains("branch-details__main-heading"))
                                         ?.InnerText.Trim();
                    //var divAddress = html.Descendants("div")
                    //                            .FirstOrDefault(div => div.GetAttributeValue("class", "").Contains("block-content clearfix"));
                    //var branchAddress = divAddress.Descendants("div")
                    //                            .FirstOrDefault(div => div.GetAttributeValue("class", "").Contains("address"))
                    //                            ?.InnerText.Trim();
                    var addressNode = html.SelectSingleNode("//div[@class='address']");
                    string address = addressNode?.InnerText.Trim();
                    string normalizedAddress = Regex.Replace(address, @"\s+", " ");
                    var regex = new Regex(@"[A-Z]{1,2}[0-9R][0-9A-Z]?\s?[0-9][A-Z]{2}", RegexOptions.IgnoreCase);
                    var match = regex.Match(normalizedAddress);
                    var branchPostcode = match.Success ? match.Value.ToUpper() : null;
                    var branchPhone = html.Descendants("a")
                                                .FirstOrDefault(a => a.GetAttributeValue("class", "").Contains("phone"))
                                                ?.InnerText.Trim();
                    var newBranch = new Branch
                    {
                        BranchUrl = branch.BranchUrl,
                        BranchName = branchName,
                        BranchAddress = normalizedAddress,
                        BranchPostcode = branchPostcode,
                        BranchPhone = branchPhone,
                    };

                    using (AppDbContext context = new AppDbContext())
                    {
                        context.Branches.Add(newBranch);
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
                        Url = branch.BranchUrl,
                        ErrorMessage = ex.Message
                    };
                    context.FailedItems.Add(failedItem);
                    context.SaveChanges();
                    Logger.Error($"Error scraping branch {branch.BranchName}: {ex.Message}");
                }
            }
        }

        public static void RetryScrape()
        {
            using (AppDbContext context = new AppDbContext())
            {
                foreach (var branch in RetryBranches)
                {
                    try
                    {
                        // Simulate retry scraping logic here
                        Console.WriteLine($"Retrying scrape for branch: {branch.BranchName} at {branch.BranchUrl}");

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error retrying branch {branch.BranchName}: {ex.Message}");
                        // Optionally, you can log this error or handle it differently
                    }
                }
                // Clear the retry list after processing
                RetryBranches.Clear();
            }
        }
    }
}
