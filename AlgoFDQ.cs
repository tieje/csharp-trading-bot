using System;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Alpaca.Markets;
using System.Collections.Generic;
using System.Threading;
using System.Globalization;

namespace StockTrading
{
    class FirstDayQuarterAlgorithm : DatabaseManagement
    {
        public DatabaseManagement sqlitedb = new DatabaseManagement();
        public ContextProvider conn = new ContextProvider();
        public string unstableSymbol = "TSLA";
        public string stableSymbol = "CGW";        
        // the step controller is the entry point of the algorithm. It will be called at the end of each Step to evaluate the step.
        // it will check which step the algorithm is on
        public async void FDQEntry()
        {
            using(conn.alpacaAccountApi = Environments.Paper.GetAlpacaTradingClient(new SecretKey(conn.API_KEY1, conn.API_SECRET1)))
            {
                bool MarketOpen = (await conn.alpacaAccountApi.GetClockAsync()).IsOpen;
                Console.WriteLine(MarketOpen.ToString());
                Console.WriteLine("Waiting until market is open.");
                while (MarketOpen != true)
                {
                    Thread.Sleep(60 * 1000);
                    MarketOpen = (await conn.alpacaAccountApi.GetClockAsync()).IsOpen;
                }
                Console.WriteLine("Market is open.");
            }
            FDQStepController();
        }
        public void FDQStepController()
        {
            AlgorithmName = "FDQ";
            StepCounter = sqlitedb.StepReader(AlgorithmName);
            equity_access_percent = Convert.ToDecimal(sqlitedb.ReadStateTable("equity_access_percent","FDQ")) / 100m;
            if (StepCounter == "1")
            {
                Step1LookForQuarter(equity_access_percent, AlgorithmName);
            }
            else if (StepCounter == "2")
            {
                Step2BuyTSLA(equity_access_percent, AlgorithmName);
            }
            else if (StepCounter == "3")
            {
                Step3SellTSLA(AlgorithmName);
            }
            else if (StepCounter == "4")
            {
                Step4BuyStableSymbolAtLimitPrice(equity_access_percent, AlgorithmName);
            }
            else
            {
                Console.WriteLine("Check the step for " + AlgorithmName);
            }
        }
        public async void Step1LookForQuarter(decimal equity_access_percent, string AlgorithmName)
        {
            Console.WriteLine("Beginning Step 1 of AlgoFDQ");
            // variables needed
            string websitesQuarter = "";
            // bool checkStableSymbol;
            // define browser
            string ChromeDriverPath = @".";
            ChromeOptions chromeoptions = new ChromeOptions();
            chromeoptions.AddArgument("headless");
            // scrape Tesla investor website
            using (IWebDriver driver = new ChromeDriver(ChromeDriverPath, chromeoptions))
            {
                try
                {
                    // xpath for first quarter appearance. This should not change.
                    string TeslaLatestQuarterXpath = "//*[@id='quarterly-disclosure-panel']/div/div[1]/table/tbody[1]/tr[1]/td[3]/div";

                    // connect to https://ir.tesla.com/
                    driver.Navigate().GoToUrl("https://ir.tesla.com/");
                    // get the latest Tesla quarter report
                    websitesQuarter = driver.FindElement(By.XPath(TeslaLatestQuarterXpath)).Text;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Something went wrong with the web scraper for FDQ. " + e.Message);
                }
            }
            //if web scraping doesn't find new quarter and if the max amount of money allotted for this algorithm is not invested in CGW, buy CGW
            // if web scraping does find new quarter, sell all CGW and move to step 2
            string desiredQuarter = sqlitedb.ReadStateTable("input",AlgorithmName);
            Console.WriteLine("Web Quarter: "+websitesQuarter + "\n" + "DB quarter: "+desiredQuarter);
            if (websitesQuarter == desiredQuarter)
            {   Console.WriteLine("Quarters are equal");
                // check if there is any CGW in my portfolio
                // Opening API Connection
                using(conn.alpacaAccountApi = Environments.Paper.GetAlpacaTradingClient(new SecretKey(conn.API_KEY1, conn.API_SECRET1)))
                {
                    IOrder order;
                    try
                    {
                        // get the Position Object
                        IPosition stableSymbolPosition = await conn.alpacaAccountApi.GetPositionAsync(stableSymbol);
                        // get current price
                        decimal stableSymbolValue = stableSymbolPosition.AssetCurrentPrice;
                        // record current price as limit price
                        sqlitedb.UpdateStateTable("limit_price", stableSymbolValue.ToString(), "FDQ");
                        // get current quantity of CGW
                        long stableSymbolQuantity = stableSymbolPosition.Quantity;
                        // sell max quantity of CGW
                        order = await conn.alpacaAccountApi.PostOrderAsync(OrderSide.Sell.Market(stableSymbol, stableSymbolQuantity));
                        // update the order ID
                        sqlitedb.UpdateStateTable("previous_step_order_id", order.OrderId.ToString(), "FDQ");
                    }
                    catch (Alpaca.Markets.RestClientErrorException e) when (e.Message == "position does not exist")
                    {
                        Console.WriteLine("There is no quantity of " + stableSymbol + "so we will continue to step 2, which is buying the TSLA stock with the money given.");
                    }
                    finally
                    {
                        // At the very end, update the State table
                        // update state table output_step_state
                        sqlitedb.UpdateStateTable("output","2",AlgorithmName);
                        // update input_data_state column to next quarter
                        sqlitedb.UpdateStateTable("input",NextQuarter(websitesQuarter),AlgorithmName);
                        // update the fdq_days_after_quarter to zero
                        sqlitedb.UpdateStateTable("fdq_days_after_quarter", DateTime.Now.ToString("M/d/yyyy"), "FDQ");
                    }
                }
                // Call the Step Controller to continue algorithm
                FDQStepController();
            }
            // if max money allotted for this algorithm is not invested already, then make sure it's invested
            else
            {
                // The total money left to invest is (Budget - Assets invested) left to invest
                using(conn.alpacaAccountApi = Environments.Paper.GetAlpacaTradingClient(new SecretKey(conn.API_KEY1, conn.API_SECRET1)))
                using(conn.alpacaDataApi = Environments.Paper.GetAlpacaDataClient(new SecretKey(conn.API_KEY1, conn.API_SECRET1)))
                {
                    var account = await conn.alpacaAccountApi.GetAccountAsync();
                // Budget = (Equity * Percentage Multipliers)
                    decimal equity = account.Equity;
                    decimal budget = equity * equity_access_percent;
                    decimal budgetCash = 0;
                    decimal stableSymbolValue = 0;
                    // equity = cash + Long_Market_value + Short_market_value
                    // cash left in budget = budget - value of stable symbol stock in Portfolio
                    try
                    {
                        IPosition stableSymbolPosition = await conn.alpacaAccountApi.GetPositionAsync(stableSymbol);
                        decimal stableSymbolPortfolioMarketValue = stableSymbolPosition.MarketValue;
                        decimal Short_market_value = (await conn.alpacaAccountApi.GetAccountAsync()).ShortMarketValue;
                        budgetCash = budget - stableSymbolPortfolioMarketValue - Short_market_value;
                        stableSymbolValue = stableSymbolPosition.AssetCurrentPrice;
                    }
                    catch (Alpaca.Markets.RestClientErrorException e) when (e.Message == "position does not exist")
                    {
                        Console.WriteLine(e.Message + " for " + stableSymbol);
                        stableSymbolValue = (await conn.alpacaDataApi.GetLastQuoteAsync(stableSymbol)).AskPrice;
                        budgetCash = budget;
                    }
                    finally
                    {
                        // if the price of CGW is less than the money I have left to invest, buy maximum floored-integer amount of CGW
                        if (stableSymbolValue < budgetCash)
                        {
                            long stableSymbolBuyQuantity = Convert.ToInt64(Math.Floor(budgetCash / stableSymbolValue));
                            if (stableSymbolBuyQuantity > 0)
                            {
                                await conn.alpacaAccountApi.PostOrderAsync(OrderSide.Buy.Market(stableSymbol, stableSymbolBuyQuantity));
                                Console.WriteLine("Bought "+stableSymbolBuyQuantity.ToString() + " shares of "+stableSymbol+" to maximize investing");
                            }
                            else
                            {
                                Console.WriteLine("Did not buy stable symbol due to lack of funds.");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Did not buy stable symbol due to lack of funds.");
                        }
                    }
                }
            }
            Console.WriteLine("Completed Step 1 of AlgoFDQ.");
        }
        public async void Step2BuyTSLA(decimal equity_access_percent, string AlgorithmName)
        {
            decimal initialInvestment;
            Guid previousStepTradeID;
            // Buy Tesla on margin. A minimum of $2000 in equity will be needed for this account. If the minimum equity is not in the account, wait until the trade is completed before buying it.
            CultureInfo localculture = new CultureInfo("en-US");
            DateTime dbdate = Convert.ToDateTime(sqlitedb.ReadStateTable("fdq_days_after_quarter", AlgorithmName), localculture);
            Console.WriteLine("Reached a date while-loop at step 2. If nothing prints after this, then it's ok to close the script.");
            TimeSpan interval = DateTime.Now - dbdate;
            while ((interval).TotalDays <= 2)
            {
                Thread.Sleep(60 * 60 * 1000);
                interval = DateTime.Now - dbdate;
            }
            Console.WriteLine("Made it out of the date while-loop.");
            using(conn.alpacaAccountApi = Environments.Paper.GetAlpacaTradingClient(new SecretKey(conn.API_KEY1, conn.API_SECRET1)))
            using(conn.alpacaDataApi = Environments.Paper.GetAlpacaDataClient(new SecretKey(conn.API_KEY1, conn.API_SECRET1)))
            {
                var account = await conn.alpacaAccountApi.GetAccountAsync();
                decimal equity = account.Equity;
                decimal budget = equity * equity_access_percent;
                decimal unstableSymbolValue = (await conn.alpacaDataApi.GetLastQuoteAsync(unstableSymbol)).AskPrice;
                long buymaxbudget = Convert.ToInt64(Math.Floor(budget/unstableSymbolValue));
                previousStepTradeID = new Guid(sqlitedb.ReadStateTable("previous_step_order_id", AlgorithmName));

                // equity is less than 2000 therefore the algorithm will start when the trade has been completed.
                if (equity < 2000)
                {
                    // check status of the sell order in the previous step to see if it has gone through. We're going to need to wait for the trade to go through.
                    string CheckStableSymbolLastTrade = (await conn.alpacaAccountApi.GetOrderAsync(previousStepTradeID)).OrderStatus.ToString();
                    while (CheckStableSymbolLastTrade.ToLower() != "filled")
                    {
                        Thread.Sleep(60 * 1000);
                        CheckStableSymbolLastTrade = (await conn.alpacaAccountApi.GetOrderAsync(previousStepTradeID)).OrderStatus.ToString();
                    }
                }
                IOrder order = await conn.alpacaAccountApi.PostOrderAsync(OrderSide.Buy.Market(unstableSymbol, buymaxbudget));
                unstableSymbolValue = (await conn.alpacaDataApi.GetLastQuoteAsync(unstableSymbol)).AskPrice;
                previousStepTradeID = order.OrderId;
                string CheckUnstableSymbolLastTrade = (await conn.alpacaAccountApi.GetOrderAsync(previousStepTradeID)).OrderStatus.ToString();
                Console.WriteLine("Checking if buying " +unstableSymbol+ " trade has been filled.");

                while (CheckUnstableSymbolLastTrade.ToLower() != "filled")
                {
                    Thread.Sleep(60 * 1000);
                    CheckUnstableSymbolLastTrade = (await conn.alpacaAccountApi.GetOrderAsync(previousStepTradeID)).OrderStatus.ToString();
                }
                initialInvestment = unstableSymbolValue * Convert.ToDecimal(buymaxbudget);
                Console.WriteLine(initialInvestment.ToString()+" is the initial investment.");
            }
            sqlitedb.UpdateStateTable("previous_step_order_id", previousStepTradeID.ToString(),AlgorithmName);
            // Record Initial investment of Tesla to calculate ROI later
            sqlitedb.UpdateStateTable("initial_investment", initialInvestment.ToString(),AlgorithmName);
            // update state table output_step_state
            sqlitedb.UpdateStateTable("output","3",AlgorithmName);
            Console.WriteLine("Completed Step 2 of FDQ.");
            FDQStepController();
        }

        public async void Step3SellTSLA(string AlgorithmName)
        {
            CultureInfo localculture = new CultureInfo("en-US");
            DateTime dbdate = Convert.ToDateTime(sqlitedb.ReadStateTable("fdq_days_after_quarter", AlgorithmName), localculture);
            Console.WriteLine("Reached a date while-loop at step 3. If nothing prints after this, then it's ok to close the script.");
            while ((DateTime.Now - dbdate).TotalDays <= 3)
            {
                Thread.Sleep(24 * 60 * 60 * 1000);
            }
            Console.WriteLine("Made it out of the date while-loop.");
            decimal original_sellprice;
            decimal ROI;
            string orderid;
            // Sell Tesla for a profit when trade is Filled.
            using(conn.alpacaAccountApi = Environments.Paper.GetAlpacaTradingClient(new SecretKey(conn.API_KEY1, conn.API_SECRET1)))
            using(conn.alpacaDataApi = Environments.Paper.GetAlpacaDataClient(new SecretKey(conn.API_KEY1, conn.API_SECRET1)))
            {
                Guid previousStepTradeID = new Guid(sqlitedb.ReadStateTable("previous_step_order_id", AlgorithmName));
                decimal step2buyTSLAPrice = (await conn.alpacaDataApi.GetLastQuoteAsync(unstableSymbol)).AskPrice;
                string CheckUnstableSymbolLastTrade = (await conn.alpacaAccountApi.GetOrderAsync(previousStepTradeID)).OrderStatus.ToString();  
                while (CheckUnstableSymbolLastTrade.ToLower() != "filled")
                {
                    Thread.Sleep(60 * 1000);
                    CheckUnstableSymbolLastTrade = (await conn.alpacaAccountApi.GetOrderAsync(previousStepTradeID)).OrderStatus.ToString();
                }
                Console.WriteLine("Buying of "+unstableSymbol+" was completed.");
                decimal percent_desired_profit = 1.01M;
                decimal profitLimit = Math.Round((step2buyTSLAPrice * percent_desired_profit), 2);
                long unstableSymbolQuantityInAccount = (await conn.alpacaAccountApi.GetPositionAsync(unstableSymbol)).Quantity;
                var order = await conn.alpacaAccountApi.PostOrderAsync(OrderSide.Sell.Limit(unstableSymbol, unstableSymbolQuantityInAccount, profitLimit));
                orderid = order.OrderId.ToString();
                original_sellprice = Math.Round(Convert.ToDecimal(sqlitedb.ReadStateTable("initial_investment", AlgorithmName)) / Convert.ToDecimal(unstableSymbolQuantityInAccount), 2);
                // Calculate ROI and record it
                decimal initial_investment = Convert.ToDecimal(sqlitedb.ReadStateTable("initial_investment", AlgorithmName));
                ROI = (original_sellprice / initial_investment) * 100M;
                sqlitedb.InsertROI(ROI.ToString(),AlgorithmName);
                // update state table output_step_state
                sqlitedb.UpdateStateTable("previous_step_order_id", orderid,AlgorithmName);
                sqlitedb.UpdateStateTable("output","4",AlgorithmName);
            }
            FDQStepController();
        }
        public async void Step4BuyStableSymbolAtLimitPrice(decimal equity_access_percent, string AlgorithmName)
        {
            Guid previousStepTradeID = new Guid(sqlitedb.ReadStateTable("previous_step_order_id", AlgorithmName));
            using(conn.alpacaDataApi = Environments.Paper.GetAlpacaDataClient(new SecretKey(conn.API_KEY1, conn.API_SECRET1)))
            using(conn.alpacaAccountApi = Environments.Paper.GetAlpacaTradingClient(new SecretKey(conn.API_KEY1, conn.API_SECRET1)))
            {
                string CheckUnstableSymbolLastTrade = (await conn.alpacaAccountApi.GetOrderAsync(previousStepTradeID)).OrderStatus.ToString();
                Console.WriteLine("Waiting on the target limit price to sell.");
                while (CheckUnstableSymbolLastTrade.ToLower() != "filled")
                {
                    Thread.Sleep(60 * 1000);
                    CheckUnstableSymbolLastTrade = (await conn.alpacaAccountApi.GetOrderAsync(previousStepTradeID)).OrderStatus.ToString();
                }
                Console.WriteLine("Sell limit price found for step 4 of FDQ. Trade was completed.");
                // Buy stable symbol at limit price that was recorded when you sold it. You might need to buy it on margin.
                decimal limit_price = Convert.ToDecimal(sqlitedb.ReadStateTable("limit_price", AlgorithmName));
                IAccount account = await conn.alpacaAccountApi.GetAccountAsync();
                decimal equity = account.Equity;
                decimal budget = equity * equity_access_percent;
                decimal stableSymbolValue = (await conn.alpacaDataApi.GetLastQuoteAsync(stableSymbol)).AskPrice;
                if (stableSymbolValue < budget)
                {
                    long stableSymbolBuyQuantity = Convert.ToInt64(Math.Floor(budget / stableSymbolValue));
                    if (stableSymbolBuyQuantity > 0)
                    {
                        IOrder order = await conn.alpacaAccountApi.PostOrderAsync(OrderSide.Buy.Limit(stableSymbol, stableSymbolBuyQuantity, limit_price));
                        Guid orderid = order.OrderId;
                        string CheckStableSymbolLastTrade = (await conn.alpacaAccountApi.GetOrderAsync(orderid)).OrderStatus.ToString();
                        Console.WriteLine("Waiting on the target limit price to buy.");
                        while (CheckStableSymbolLastTrade.ToLower() != "filled")
                        {
                            Thread.Sleep(60 * 1000);
                            CheckStableSymbolLastTrade = (await conn.alpacaAccountApi.GetOrderAsync(previousStepTradeID)).OrderStatus.ToString();
                        }
                        Console.WriteLine("Buy limit price found for step 4 of FDQ. Trade was completed.");
                    }
                }
            }
            // update state table output_step_state
            sqlitedb.UpdateStateTable("output","1",AlgorithmName);
            FDQStepController();
        }
        // helper function for determining the next quarter to save in the state. This may have more easily been completed by if-statements
        public string NextQuarter(string currentQuarter)
        {
            Dictionary<string, string> quarterdict = new Dictionary<string, string>();
            quarterdict.Add("Q1", "Q2");
            quarterdict.Add("Q2","Q3");
            quarterdict.Add("Q3","Q4");
            quarterdict.Add("Q4","Q1");
            return quarterdict[currentQuarter];
        }
    }
}