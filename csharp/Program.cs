using System;
using System.Text.Json;
using System.Net.Http;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

// useful links:
// https://stackoverflow.com/questions/18013523/when-correctly-use-task-run-and-when-just-async-await
// https://docs.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/consuming-the-task-based-asynchronous-pattern#using-the-built-in-task-based-combinators
// https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/chaining-tasks-by-using-continuation-tasks

namespace ConcurrencyExample
{
    public class HackerNewsAPI
    {
        public static readonly string urlBase =
            "https://hacker-news.firebaseio.com/v0/";

        public static string TopStoriesURL => urlBase + "topstories.json";

        public static string ConstructItemURL(string part) => urlBase + "item/" + part + ".json";

#pragma warning disable IDE1006 // Naming Styles. This is to deserialize JSON

        public class Item
        {
            public int id { get; set; }
            public string url { get; set; } = "";
        }

#pragma warning restore IDE1006 // Naming Styles
    }

    internal class Program
    {
        private static readonly HttpClient client = new HttpClient();

        // size of a chunk, det'd by trial and error
        private static readonly int ChunkSize = 30000;

        // Does a word count recursively, from left to right
        // Note: range is [left, right)
        private static int WordCountHelper(string body, int left, int right)
        {
            int size = right - left;
            if (size < ChunkSize)
            {
                return body.Substring(left, size).Split().Length;
            }
            else
            {
                int mid = (right + left) / 2;
                while (body[mid] != ' ') mid++; // Only split on whitespace
                // Run task in parallel, then later on await it.
                var leftTask = Task.Run(() => WordCountHelper(body, left, mid));
                int r = WordCountHelper(body, mid, right);
                return leftTask.Result + r;
            }
        }

        private static int WordCountHelperNaive(string body)
        {
            return body.Split().Length;
        }

        private static async Task<string> WordCountAsync(string url, CancellationToken token)
        {
            if (url == "") throw new ArgumentException("Url cannot be null to Word Count");
            try
            {
                string body = await client.GetStringAsync(url);
                if (token.IsCancellationRequested)
                {
                    return $"{url}: Error - Timeout, operation cancelled";
                }
                int length = WordCountHelper(body, 0, body.Length);
                if (token.IsCancellationRequested)
                {
                    return $"{url}: Error - Timeout, operation cancelled";
                }
                return $"{url}: {length} words";
            }
            catch (HttpRequestException e)
            {
                return $"{url} errored with error {e}";
            }
        }

        private static IEnumerable<Task<string>> GetTopHNUrls(int maxItems = 20)
        {
            string hackerNewsJson = client.GetStringAsync(HackerNewsAPI.TopStoriesURL).Result;
            var items = JsonSerializer.Deserialize<int[]>(hackerNewsJson);
            int n = Math.Min(maxItems, items.Length - 1);
            return Enumerable.Range(0, n)
                   .Select(async i =>
                      { // each item corresponds to a unique token, which must be fetched asynchronously
                          var token = items[i];
                          string url = HackerNewsAPI.ConstructItemURL(token.ToString());
                          string urlBody = await client.GetStringAsync(url);
                          var item = JsonSerializer.Deserialize<HackerNewsAPI.Item>(urlBody);
                          return item.url;
                      });
        }

        private static async Task Main()
        {
            await Task.WhenAll(
                GetTopHNUrls(20)
                .Select(async urlTask =>
                  {
                      try
                      {
                          var timeoutInMillis = 5000;
                          var url = await urlTask;
                          var source = new CancellationTokenSource();
                          // Cancel after some number of seconds
                          _ = Task.Delay(timeoutInMillis).ContinueWith(_ => source.Cancel());
                          Console.WriteLine(await WordCountAsync(url, source.Token));
                      }
                      catch (HttpRequestException e)
                      {
                          Console.WriteLine($"Error - urlTask experienced network failure, message: {e}");
                      }
                      catch (JsonException e)
                      {
                          Console.WriteLine($"Error - urlTask obtained invalid JSON, message: {e}");
                      }
                  })
                );
        }
    }
}