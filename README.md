This repo implements a concurrent AND parallel downloader in multiple languages. Currently, the languages it is programmed in is:
  - C#

This list is evolving!

## Why?
Because Paralleism and Concurrency are two completely different issues, and I hope to demonstrate the difference through this simple example repo. Also, it's a good task to try building to test out new languages.


## The specification
The task is a very simple Hacker News downloader. It is a command-line based launcher. On program start, it will make a connection to the Hacker News API and download the URLs of the top 20 websites. Then, it will download the HTML of those URLs, and perform a parallel split of the HTML by whitespace. After the word count is done, it is put into a queue, and printed out one by one to the console. 

Sample output:
```
https://example.com: 4000 words
```

The downloader must handle failures due to network errors and timeouts. 

1. Regarding network failures, for simplicity this program should not retry in the face of errors, but instead fail gracefully by printing something to stdout.
1. Regarding timeouts, the entire operation, from fetching URL to HTML to String Splitting must complete in 5 seconds. If not, the program should print an error message for that URL (but still continue with the other URLs).

Sample output:
```
https://exampleNetworkFailure.com: Error - Network failure
https://exampleTimeout.com: Timout - Operation not completed in 5 seconds.
```

The dataflow graph looks like this, for 2 URLs: 
Each arm of the graph is performed independently of the others.
```
      Obtain top 20 URLs from API
      _________________________
          |                 |
          |                 |
        URL 1             URL 2
          |                 |
          |                 |
         HTML              HTML
        |   |             |    |
        |   |             |    |
    Split  Split        Split  Split
        |   |             |    |
        |   |             |    |
        -----             ------
      Word Count         Word Count
          |                  |
          |                  |
          -------------------
                Queue
                  |
                Print to
                Console
```

## How does this show Parallel vs Concurrency?
I have a WIP blog post about this.



