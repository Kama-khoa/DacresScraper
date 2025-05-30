using RealEstateScraper;
using DatabaseContext.Models;
using System;
using Microsoft.EntityFrameworkCore;
using DatabaseContext;

namespace BranchScraping
{
    public class Program
    {
        public static void Run(int numberOfThread, int retryTimes)
        {
            Console.WriteLine($"Start Branch Scrape");

            using (AppDbContext context = new AppDbContext())
            {
                var newBranches = context.NewBranchUrls.ToList();
                ScrapeBranch.Branches = newBranches;
                ScrapeBranch.RetryBranches = new List<NewBranchUrl>();
            }

            Task[] backgroundTasks = new Task[numberOfThread];
            for (int i = 0; i < numberOfThread; i++)
            {
                backgroundTasks[i] = Task.Run(() => ScrapeBranch.StartScrape());

            }

            //// wait synchronously

            Task.WaitAll(backgroundTasks);

            for (int time = 0; time < retryTimes; time++)
            {
                //retry to scrape the failed listing one more time            
                ScrapeBranch.Branches = ScrapeBranch.RetryBranches;
                ScrapeBranch.RetryBranches = new List<NewBranchUrl>();

                //if it's the last time we retry, then we mark the isRetry flag to be false
                if (time == retryTimes - 1)
                {
                    ScrapeBranch.IsRetry = true;
                }

                backgroundTasks = new Task[numberOfThread];
                for (int i = 0; i < numberOfThread; i++)
                {
                    backgroundTasks[i] = Task.Run(() => ScrapeBranch.StartScrape());
                }
                //wait for all threads to complete
                Task.WaitAll(backgroundTasks);
            }

            Console.WriteLine("End Branch Scrape");
        }
    }
}