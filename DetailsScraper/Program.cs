using DatabaseContext;
using log4net;

namespace DetailsScraper
{
    public class Program
    {
        public static void Run(int numberThreads, ILog logger, int retryTimes)
        {
            logger.Info("Starting the details scraper with " + numberThreads + " threads.");

            ScraperDetail.Logger = logger;
            using (AppDbContext context = new AppDbContext())
            {
                var newListings = context.NewListingUrls.ToList();
                ScraperDetail.Listings = newListings;
                ScraperDetail.RetryListings = new List<BasicListingUrl>();
            }

            Task[] backgroundTasks = new Task[numberThreads];
            for (int i = 0; i < numberThreads; i++)
            {
                backgroundTasks[i] = Task.Run(() => ScraperDetail.StartScrape());
            }

            Task.WaitAll(backgroundTasks);

            for (int time = 0; time < retryTimes; time++)
            {
                logger.Info("Retry the failed Listing one more time");
                ScraperDetail.Listings = ScraperDetail.RetryListings;
                ScraperDetail.RetryListings = new List<BasicListingUrl>();

                if (time == retryTimes - 1)
                {
                    ScraperDetail.IsRetry = true;
                }

                backgroundTasks = new Task[numberThreads];
                for (int i = 0; i < numberThreads; i++)
                {
                    backgroundTasks[i] = Task.Run(() => ScraperDetail.StartScrape());
                }

                Task.WaitAll(backgroundTasks);
            }


            logger.Info("End Listing Scrape");
        }
    }
}