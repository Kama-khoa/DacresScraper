using DotNetEnv;

namespace Utilities
{
    public class AppConfig
    {
        public string ProxyHost { get; private set; }
        public int ProxyPort { get; private set; }
        public string ProxyUsername { get; private set; }
        public string ProxyPassword { get; private set; }
        public string DbConnectionString { get; private set; }

        public AppConfig()
        {
            // Load biến môi trường từ file .env
            Env.Load("G:/Works/Dacres/DacresCrawler/.env");

            ProxyHost = Environment.GetEnvironmentVariable("PROXY_HOST");
            ProxyPort = int.Parse(Environment.GetEnvironmentVariable("PROXY_PORT") ?? "0");
            ProxyUsername = Environment.GetEnvironmentVariable("PROXY_USERNAME");
            ProxyPassword = Environment.GetEnvironmentVariable("PROXY_PASSWORD");
            DbConnectionString = Environment.GetEnvironmentVariable("DB_CONNECTION");
        }
        public string GetProxyUrl() => $"http://{ProxyHost}:{ProxyPort}";
    }
}
