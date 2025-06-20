﻿using RealEstateScraper;
using DatabaseContext.Models;
using System;
using Microsoft.EntityFrameworkCore;
using DatabaseContext;
using log4net;

namespace BranchScraping
{
    public class Program
    {
        private static string url = "https://dacres.co.uk/contact-us/";
        public static void Run(ILog logger, int retryTimes)
        {
            logger.Info($"Start Branch Scrape");

            ScrapeBranch.Logger = logger;
            using (AppDbContext context = new AppDbContext())
            {
                var newListings = context.NewBranchUrls.ToList();
                ScrapeBranch.Branches = newListings;
                ScrapeBranch.RetryBranches = new List<NewBranchUrl>();
            }

            //Task[] backgroundTasks = new Task[numberOfThread];
            //for (int i = 0; i < numberOfThread; i++)
            //{
            //    backgroundTasks[i] = Task.Run(() => ScrapeBranch.StartScrape());

            //}

            //Task.WaitAll(backgroundTasks);

            ScrapeBranch.StartScrape();

            for (int time = 0; time < retryTimes; time++)
            {
                logger.Info("Retry the failed Listing one more time");
                ScrapeBranch.Branches = ScrapeBranch.RetryBranches;
                ScrapeBranch.RetryBranches = new List<NewBranchUrl>();

                //if it's the last time we retry, then we mark the isRetry flag to be false
                if (time == retryTimes - 1)
                {
                    ScrapeBranch.IsRetry = true;
                }

                ScrapeBranch.StartScrape();
            }

            logger.Info("End Branch Scrape");
        }
    }
}