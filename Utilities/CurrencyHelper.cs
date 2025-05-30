namespace Utilities 
{ 
	public static class CurrencyHelper
	{
		public static string CurrencySymbolToIsoCode(string symbol)
		{
			return symbol switch
			{
				"£" => "GBP",
				"$" => "USD",
				"€" => "EUR",
				"¥" => "JPY",
				"₫" => "VND",
				"₹" => "INR",
				_ => "UNKNOWN",
			};
		}
	}
}