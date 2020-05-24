using System;
using System.Text.Json;
using System.Net.Http;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

// useful links:
// https://stackoverflow.com/questions/18013523/when-correctly-use-task-run-and-when-just-async-await
// https://docs.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/consuming-the-task-based-asynchronous-pattern#using-the-built-in-task-based-combinators
//

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
        private static readonly int ChunkSize = 20000; // size of a chunk

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

        private static async Task<int> WordCountAsync(string url)
        {
            if (url == "") return 0;
            string body = await client.GetStringAsync(url);
            return WordCountHelper(body, 0, body.Length);
        }

        private static IEnumerable<Task<string>> GetTopHNUrls(int maxItems = 10)
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
                          return item.url; // update the global result array in parallel
                      });
        }

        private static async Task Main()
        {
            // Throws: HTTP exception, JSON exception. In both cases, we want to halt the program
            // as this indicates a design error.
            await Task.WhenAll(
                GetTopHNUrls(10)
                .Select(async urlTask =>
                  {
                      var url = await urlTask;
                      int wordCount = await WordCountAsync(url);
                      Console.WriteLine((url, wordCount));
                  })
                );
        }
    }
}