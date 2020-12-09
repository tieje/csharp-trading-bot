/*
The main purpose of the Context Provider is to either grab information that algorithms will use as input for their conditionals to activate, OR to activate the web scraper that will grab information for the algorithm to activate.
*/
using System;
using Alpaca.Markets;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace StockTrading
{
    public class ContextProvider : IDisposable
    {
        private string API_KEY = "Alpaca API key here";
        private string API_SECRET = "Alpaca API secret key here";
        public IAlpacaTradingClient alpacaAccountApi;
        public IAlpacaDataClient alpacaDataApi;
        public IAlpacaStreamingClient alpacaAccountStream;
        public string API_KEY1 { get => API_KEY; }
        public string API_SECRET1 { get => API_SECRET; }
        
        /*
        public async Task ProvideContextAsync()
        {

            // Insert break here
            // Connecting Streams
            alpacaAccountStream = Environments.Paper
                .GetAlpacaStreamingClient(new SecretKey(API_KEY1, API_SECRET1));
        // My Account information stream. Only deals with updates on trade statuses.
            alpacaAccountStream.ConnectAndAuthenticateAsync().Wait();
            alpacaAccountStream.OnTradeUpdate += HandleTradeUpdate;
            //alpacaAccountStream.OnAccountUpdate += HandleAccountUpdate;

            // Test account information account information.
            // Delete this later
            var account = await alpacaAccountApi.GetAccountAsync();
            /*
            Console.WriteLine(account.BuyingPower + " is your current buying power.");
            // Universally useful information
            // Time info
            var calendars = (await alpacaAccountApi
                .ListCalendarAsync(new CalendarRequest().SetTimeInterval(DateTime.Today.GetInclusiveIntervalFromThat())))
                .ToList();
            var calendarDate = calendars.First().TradingDateUtc;
            var closingTime = calendars.First().TradingCloseTimeUtc;
            var today = DateTime.Today;
        
        }

        // Stream event handlers
        // For now, both event handlers will simple print information based on the trade information.
        // Trade Update event handler
        */
        public void HandleTradeUpdate(ITradeUpdate trade)
        {
            switch (trade.Event)
            {
                case TradeEvent.Fill:
                    Console.WriteLine("Trade filled.");
                    break;
                case TradeEvent.Rejected:
                    Console.WriteLine("Trade rejected.");
                    break;
                case TradeEvent.Canceled:
                    Console.WriteLine("Trade canceled.");
                    break;
                    // https://alpaca.markets/docs/api-documentation/api-v2/streaming/
                    // Other events can be included and potential events are defined in the link above
            }
        }
        /*
        // Account update event handler
        // Looking around, I can't find the API for this... and I hope to never receive an account update because according to the following interface:
        // https://github.com/alpacahq/alpaca-trade-api-csharp/blob/develop/Alpaca.Markets/Interfaces/IAccountUpdate.cs
        // There is a shifty DeletedAtUtc property meaning this will tell me if my account got deleted.
        private void HandleAccountUpdate(IAccountUpdate account)
        {
            switch (account.Event)
            {
                case 
            }
        }
*/
        // Dispose? I need to figure out dispose. I'm doing this for all external connections. I know it's about calling garbage collection.
        public void Dispose()
        {
            alpacaAccountApi?.Dispose();
            alpacaAccountStream?.Dispose();
        }
    }
}