using DatabaseContext;
using DatabaseContext.Models;
using DotNetEnv;
using log4net;
using log4net.Config;
using Microsoft.EntityFrameworkCore;
using System;
using System.Reflection;

namespace RealEstateScraper
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));
            var logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            int numThreads = 5; // Number of threads to use for scraping

            int RetryTimes = 1;

            using (var context = new AppDbContext())
            {
                try
                {
                    //context.Database.ExecuteSqlRaw("TRUNCATE TABLE Properties");

                    //context.Database.ExecuteSqlRaw("TRUNCATE TABLE Branches");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Cannot delete Properties");
                }
            }

            logger.Info("Starting the scraper with " + numThreads + " threads.");
            //ScraperService.Start(numThreads, logger);

            DetailsScraper.Program.Run(numThreads, logger, RetryTimes);

            //BranchScraping.Program.Run(numThreads, logger, RetryTimes);
        }
    }
}