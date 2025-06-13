using BranchScraping.DTOs;
using DatabaseContext;
using DatabaseContext.Models;
using HtmlAgilityPack;
using log4net;
using Newtonsoft.Json;
using System.Net;
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

        public static string CrawlPhoneFromBranchPage(string branchUrl)
        {
            try
            {
                string page = string.Empty;
                using (WebClient webClient = new WebClient())
                {
                    //AppConfig appConfig = new AppConfig();

                    //webClient.Proxy = new WebProxy(appConfig.ProxyUrl)
                    //{
                    //    Credentials = new NetworkCredential(appConfig.ProxyUsername, appConfig.ProxyPassword)
                    //};
                    webClient.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/69.0.3497.92 Safari/537.36";

                    page = webClient.DownloadString(branchUrl);
                    if (page == null)
                    {
                        return null;
                    }
                }
                HtmlDocument htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(page);

                var html = htmlDocument.DocumentNode.Descendants("body").First();
                if (html != null)
                {
                    var branchPhone = html.Descendants("a")
                                                .FirstOrDefault(a => a.GetAttributeValue("class", "").Contains("phone"))
                                                ?.InnerText.Trim();
                    return branchPhone;
                }
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error crawling phone from {branchUrl}: {ex.Message}");
                return null;
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
                    //AppConfig appConfig = new AppConfig();

                    //webClient.Proxy = new WebProxy(appConfig.ProxyUrl)
                    //{
                    //    Credentials = new NetworkCredential(appConfig.ProxyUsername, appConfig.ProxyPassword)
                    //};
                    webClient.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/69.0.3497.92 Safari/537.36";

                    page = webClient.DownloadString(branch.BranchUrl);
                    if (page == null)
                    {
                        return;
                    }
                }
                var branchesData = ExtractBranchesFromScript(page);

                if (branchesData != null && branchesData.Any())
                {
                    using (AppDbContext context = new AppDbContext())
                    {
                        foreach (var branchDto in branchesData)
                        {
                            string branchPhone = null;
                            if (!string.IsNullOrEmpty(branchDto.BranchUrl))
                            {
                                var fullBranchUrl = branchDto.BranchUrl.StartsWith("http") ?
                                                   branchDto.BranchUrl :
                                                   $"https://dacres.co.uk{branchDto.BranchUrl}";
                                branchPhone = CrawlPhoneFromBranchPage(fullBranchUrl);
                            }
                            // Extract postcode from address
                            var branchPostcode = ExtractPostcode(branchDto.Address);

                            var newBranch = new Branch
                            {
                                BranchUrl = !string.IsNullOrEmpty(branchDto.BranchUrl) ?
                                           (branchDto.BranchUrl.StartsWith("http") ? branchDto.BranchUrl : $"https://example.com{branchDto.BranchUrl}") :
                                           branch.BranchUrl,
                                BranchName = branchDto.Name,
                                BranchAddress = branchDto.Address?.Replace("\n", ", "),
                                BranchPostcode = branchPostcode,
                                BranchPhone = branchPhone,
                            };
                            context.Branches.Add(newBranch);
                        }
                        context.SaveChanges();
                        Logger.Info($"Successfully saved {branchesData.Count} branches from {branch.BranchUrl}");
                    }
                }
                else
                {
                    Logger.Warn($"No branch data found in JavaScript for {branch.BranchUrl}");
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

        private static List<BranchDto> ExtractBranchesFromScript(string pageContent)
        {
            try
            {
                // Find the JavaScript code containing the branches data
                var scriptPattern = @"Ctesius\.addConfig\('branches',\s*({.*?})\);";
                var match = Regex.Match(pageContent, scriptPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    var jsonString = match.Groups[1].Value;
                    
                    // Clean up the JSON string
                    jsonString = jsonString.Replace("\\u0026", "&");
                    
                    var branchResponse = JsonConvert.DeserializeObject<BranchResponse>(jsonString);
                    return branchResponse?.Branches ?? new List<BranchDto>();
                }

                // Alternative method: Look for the JSON data in script tags
                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(pageContent);

                var scriptNodes = htmlDocument.DocumentNode.Descendants("script")
                    .Where(node => node.InnerText.Contains("branches") && node.InnerText.Contains("lat") && node.InnerText.Contains("lng"));

                foreach (var scriptNode in scriptNodes)
                {
                    var scriptContent = scriptNode.InnerText;
                    var branchMatch = Regex.Match(scriptContent, @"""branches"":\s*\[(.*?)\]", RegexOptions.Singleline);
                    
                    if (branchMatch.Success)
                    {
                        var branchesArrayJson = $"[{branchMatch.Groups[1].Value}]";
                        branchesArrayJson = branchesArrayJson.Replace("\\u0026", "&");
                        
                        var branches = JsonConvert.DeserializeObject<List<BranchDto>>(branchesArrayJson);
                        return branches ?? new List<BranchDto>();
                    }
                }

                return new List<BranchDto>();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error extracting branches from script: {ex.Message}");
                return new List<BranchDto>();
            }
        }

        private static string ExtractPostcode(string address)
        {
            if (string.IsNullOrEmpty(address))
                return null;

            // UK postcode regex pattern
            var regex = new Regex(@"[A-Z]{1,2}[0-9R][0-9A-Z]?\s?[0-9][A-Z]{2}", RegexOptions.IgnoreCase);
            var match = regex.Match(address);
            return match.Success ? match.Value.ToUpper() : null;
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
