
using System;
using System.Threading.Tasks;
using Alpaca.Markets;

namespace StockTrading
{
    internal static class Program
    {
        public static async Task Main()
        {
            try
            {
                // Each Algorithm will have their own entry point.
                // FDQ algorithm entry point
                var FDQEntryPoint = new FirstDayQuarterAlgorithm();
                await Task.Run(() => FDQEntryPoint.FDQEntry());
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }

            Console.Read();
        }
    }
}