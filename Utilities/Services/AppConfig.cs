using DotNetEnv;

namespace Utilities
{
    public class AppConfig
    {
        public string ProxyUrl { get; private set; }
        public string ProxyUsername { get; private set; }
        public string ProxyPassword { get; private set; }
        public string DbConnectionString { get; private set; }

        public AppConfig()
        {
            // Load biến môi trường từ file .env
            Env.Load("G:/Works/Dacres/DacresCrawler/.env");

            ProxyUrl = Environment.GetEnvironmentVariable("PROXY_URL");
            ProxyUsername = Environment.GetEnvironmentVariable("PROXY_USERNAME");
            ProxyPassword = Environment.GetEnvironmentVariable("PROXY_PASSWORD");
            DbConnectionString = Environment.GetEnvironmentVariable("DB_CONNECTION");
        }
    }
}
