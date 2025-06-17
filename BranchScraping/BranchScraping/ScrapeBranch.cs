using BranchScraping.DTOs;
using DatabaseContext;
using DatabaseContext.Models;
using HtmlAgilityPack;
using log4net;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Utilities;

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
                PerformScrape(Branches);
            }
            catch (Exception ex)
            {
                Logger.Error("An error occurred during scraping: " + ex.Message);
            }
        }
        private static void PerformScrape(List<NewBranchUrl> Branches)
        {
            Logger.Info("Scraping details for branches");
            CookieContainer cookies;
            var handler = CookieSessionManager.GetHandlerWithCookies(out cookies);
            using (HttpClient client = CookieSessionManager.CreateHttpClient(handler))
            {
                var url = "https://dacres.co.uk/contact-us/";
                var html = client.GetStringAsync(url).Result;
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);
                var nodes = htmlDoc.DocumentNode.SelectNodes("//div[@class='contact-five list-item']");
                if (nodes == null)
                {
                    Logger.Error($"No branch info in Url: {url}");
                    return;
                }
                foreach (var branch in Branches)
                {
                    try
                    {
                        foreach (var item in nodes)
                        {
                            var nameNode = item.SelectSingleNode(".//h3");
                            var currentBranchName = WebUtility.HtmlDecode(nameNode?.InnerText).Trim() ?? "";

                            string matchedBranchName = null;
                            bool isTargetBranch = false;

                            if (!string.IsNullOrEmpty(currentBranchName))
                            {
                                var currentWords = currentBranchName.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                var targetWords = branch.BranchName.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                                int matchCount = currentWords.Count(word => targetWords.Contains(word));

                                if (matchCount >= 2)
                                {
                                    isTargetBranch = true;
                                    matchedBranchName = branch.BranchName;
                                }
                            }

                            if (!isTargetBranch)
                                continue;

                            var addressNodes = item.SelectNodes(".//p[not(@class)]");
                            string branchAddress = "N/A", branchPostcode = "N/A";
                            if (addressNodes != null && addressNodes.Count >= 2)
                            {
                                branchAddress = string.Join(", ", addressNodes.Take(addressNodes.Count - 1)
                                    .Select(p => p.InnerText.Trim()).Where(p => !string.IsNullOrEmpty(p)));
                                branchPostcode = addressNodes.Last().InnerText.Trim();
                            }

                            var phoneNode = item.SelectSingleNode(".//a[starts-with(@href, 'tel:')]");
                            var phoneSpan = item.SelectSingleNode(".//span[contains(@class, 'telephone')]");
                            string branchPhone = phoneNode?.GetAttributeValue("href", "")?.Replace("tel:", "").Trim() ??
                                                 phoneSpan?.InnerText.Trim() ?? "N/A";

                            string branchEmail = "N/A";
                            var emailNodes = item.SelectNodes(".//a[starts-with(@href, 'mailto:')]");
                            if (matchedBranchName.ToLower().Contains("lettings"))
                            {
                                var lettingEmail = emailNodes?.FirstOrDefault(a => a.GetAttributeValue("href", "").ToLower().Contains("letting"));
                                branchEmail = lettingEmail?.GetAttributeValue("href", "").Replace("mailto:", "").Trim() ??
                                              emailNodes?.FirstOrDefault()?.GetAttributeValue("href", "").Replace("mailto:", "").Trim() ?? "N/A";
                            }
                            else
                            {
                                branchEmail = emailNodes?.FirstOrDefault()?.GetAttributeValue("href", "").Replace("mailto:", "").Trim() ?? "N/A";
                            }

                            var newBranch = new Branch
                            {
                                BranchUrl = branch.BranchUrl,
                                BranchName = branch.BranchName,
                                BranchAddress = branchAddress,
                                BranchPostcode = branchPostcode,
                                BranchPhone = branchPhone
                            };
                            using (AppDbContext context = new AppDbContext())
                            {
                                context.Branches.Add(newBranch);
                                context.SaveChanges();
                                Logger.Info($"Successfully saved branch {branch.BranchName} from {branch.BranchUrl}");
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        if(IsRetry)
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
                        RetryBranches.Add(branch);
                    }
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
