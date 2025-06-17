// CookieSessionManager.cs
using System;
using System.Net;
using System.Net.Http;

namespace Utilities
{
	public static class CookieSessionManager
	{
		private static AppConfig appConfig = new AppConfig();

        public static HttpClientHandler GetHandlerWithCookies(out CookieContainer cookies)
		{
			cookies = new CookieContainer();
			var handler = new HttpClientHandler
			{
				
                Proxy = new WebProxy(appConfig.ProxyHost, appConfig.ProxyPort)
				{
					Credentials = new NetworkCredential(appConfig.ProxyUsername, appConfig.ProxyPassword)
				},
                CookieContainer = cookies,
				AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
				UseCookies = true,
				AllowAutoRedirect = true
			};

			return handler;
		}

		public static HttpClient CreateHttpClient(HttpClientHandler handler)
		{
			var client = new HttpClient(handler);

			// Basic headers giả lập browser thật
			client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
			client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
			client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br, zstd");
			client.DefaultRequestHeaders.Add("Accept-Language", "en-US;q=0.9,en;q=0.8");

			return client;
		}
	}
}