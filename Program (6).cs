using System.Globalization;
using System.Net;
using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

public class Prices
{
    public int Id { get; set; }
    public int TickerId { get; set; }
    public double PriceBefore { get; set; }
    public double PriceAfter { get; set; }
    //public DateTimeOffset DateBefore { get; set; }
    //public DateTimeOffset DateAfter { get; set; }
}

public class Tickers
{
    public int Id { get; set; }
    public string TickersSymbol { get; set; }
}

public class TodayCondition
{
    public int Id { get; set; }
    public int TickerId { get; set; }
    public string State { get; set; }
}
public class TickersContext : DbContext
{
    public DbSet<Tickers> Tickers { get; set; }
    public DbSet<Prices> Prices { get; set; }
    public DbSet<TodayCondition> TodayCondition { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseMySql(
            "server=localhost;user=root;password=password;database=tickers;",
            new MySqlServerVersion(new Version(8, 0, 35))
        );
    }
}
public class Program
{
    public static void Main(string[] args)
    {
        string[] tickers = File.ReadAllLines("C:\\Users\\kolob\\OneDrive\\Рабочий стол\\sem3\\laba9\\ticker1.txt");

        using (var context = new TickersContext())
        {
            FillDb(context, tickers);
            UpdateTodayCondition(context);

            Console.WriteLine("Enter a ticker symbol:");
            var tickerSymbol = Console.ReadLine();

            while (tickerSymbol != "")
            {
                var ticker = context.Tickers.FirstOrDefault(t => t.TickersSymbol == tickerSymbol);
                if (ticker == null)
                {
                    Console.WriteLine("Ticker symbol not found.");
                    return;
                }

                var todayCondition = context.TodayCondition.FirstOrDefault(c => c.TickerId == ticker.Id);
                if (todayCondition == null)
                {
                    Console.WriteLine("No data available for today.");
                    return;
                }

                Console.WriteLine($"Price for {tickerSymbol} has {todayCondition.State} today.");

                Console.WriteLine("Enter new ticker symbol or press Enter for exit:");
                tickerSymbol = Console.ReadLine();
            }
        }
    }

    public static void UpdateTodayCondition(TickersContext context)
    {
        var tickerPrices = context.Prices.ToList();
        string state;
        int todayConditionId = 1;
        foreach (var tickerPrice in tickerPrices)
        {
            if (tickerPrice.PriceBefore > tickerPrice.PriceAfter)
            {
                state = "decreased";
            }
            else
            {
                state = "increased";
            }

            var todayCondition = new TodayCondition
            {
                Id = todayConditionId++,
                TickerId = tickerPrice.TickerId,
                State = state
            };
            Console.WriteLine($"Debug");

           Console.WriteLine($"Debug: TodayCondition Id={todayCondition.Id}, TickerId={todayCondition.TickerId}, State={todayCondition.State}");
            context.TodayCondition.Add(todayCondition);
     
        }

        try
        {
            context.SaveChanges();
            

        }

        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при сохранении: {ex.Message}"); 
            
            if (ex.InnerException != null )
            {
                Console.WriteLine($"Inner {ex.InnerException}");
            }
        }
    }

    private static void FillDb(TickersContext context, string[] tickers)
    {
        for (int i = 0; i < tickers.Length; ++i)
        {
            Console.WriteLine($"Processing{tickers[i]}");
            var ticker = new Tickers
            {
                TickersSymbol = tickers[i]
            };
            context.Tickers.Add(ticker);
            context.SaveChanges();

            var arr = GetStockData(tickers[i]);
            //Console.WriteLine(arr);
            if (arr.Length >= 1)
            {
                var temp = new Prices
                {
                    TickerId = context.Tickers.FirstOrDefault(t => t.TickersSymbol == ticker.TickersSymbol).Id,
                    PriceBefore = arr[0],
                    PriceAfter = arr[1],
                    //DateBefore = DateTimeOffset.Now.Date,
                    //DateAfter = DateTimeOffset.Now.AddDays(1)
                };
                context.Prices.Add(temp);
                context.SaveChanges();
            }
        }
    }

    static double[] GetStockData(string ticker)
    {
        Console.WriteLine($"GetS{ticker}");
        long startTimestamp = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds();
        long endTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        string url = $"https://query1.finance.yahoo.com/v7/finance/download/{ticker}?period1={startTimestamp}&period2={endTimestamp}&interval=1d&events=history&includeAdjustedClose=true";

        using (WebClient client = new WebClient())
        {
            try
            {
                // download data
                string data = client.DownloadString(url);
                Console.WriteLine($"FROM{ticker}:\n{data}");

                string[] lines = data.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                double[] prices = new double[lines.Length - 1];
                for (int i = 1; i < lines.Length; i++)
                {
                    var columns = lines[i].Split(',');
                    var high = double.Parse(columns[2], CultureInfo.InvariantCulture);
                    var low = double.Parse(columns[3], CultureInfo.InvariantCulture);
                    Console.WriteLine($"Processed {ticker}: High={high},Low={low}");
                    prices[i - 1] = (high + low) / 2;
                }

                return prices;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Произошла ошибка при получении данных для акции {ticker}: {e.Message}");
                return null;
            }
        }
    }
}