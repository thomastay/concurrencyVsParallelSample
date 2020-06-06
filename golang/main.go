package main

import (
	"bufio"
	"encoding/json"
	"fmt"
	"net/http"
	"strconv"
)

// Hacker news API

var hackerNewsAPIBase = "https://hacker-news.firebaseio.com/v0/"
var topStoriesURL = hackerNewsAPIBase + "topstories.json"

func constructItemURL(item int) string {
	return hackerNewsAPIBase + "item/" + strconv.Itoa(item) + ".json"
}

// end Hacker News API

type hackerNewsItem struct {
	URL string `json:"url"`
}

func getWordCount(url string) (int, error) {
	// fmt.Printf("Getting word count for URL %s!\n", url)
	resp, err := http.Get(url)
	if err != nil {
		connErr := fmt.Errorf("%s - Error unable to connect, %s", url, err.Error())
		return -1, connErr
	}
	defer resp.Body.Close()
	// Splitting words copied from Stack Overflow
	// credit to Alex Efimov
	// https://stackoverflow.com/questions/43450113/how-to-use-bufio-scanwords
	scanner := bufio.NewScanner(resp.Body)
	// Set the split function for the scanning operation.
	scanner.Split(bufio.ScanWords)
	// Count the words.
	count := 0
	for scanner.Scan() {
		count++
	}
	if err := scanner.Err(); err != nil {
		return -1, fmt.Errorf("%s - Error in word count", url)
	}
	// no error
	return count, nil
}

func getItem(itemID int, results chan string) {
	resp, err := http.Get(constructItemURL(itemID))
	if err != nil {
		results <- fmt.Sprintf("Unable to connect to the hacker news API, error: %s\n", err.Error())
	}
	defer resp.Body.Close()
	var item hackerNewsItem
	json.NewDecoder(resp.Body).Decode(&item)

	count, err := getWordCount(item.URL)
	if err != nil {
		results <- err.Error()
	}
	results <- fmt.Sprintf("%s - %d words\n", item.URL, count)
}

func main() {
	fmt.Println("Getting the top 20 items!")
	resp, err := http.Get(topStoriesURL)
	if err != nil { // there was a HTTP GET error!
		fmt.Println("Unable to connect to the hacker news API")
		return
	}
	defer resp.Body.Close()

	var topItems []int
	json.NewDecoder(resp.Body).Decode(&topItems)

	// results is First in First Out Queue
	// that is thread safe
	results := make(chan string)

	// Launched these 20 items in parallel
	for i := 0; i < 20; i++ {
		go getItem(topItems[i], results)
	}

	// Used a queue here for concurrency
	for count := 0; count < 20; count++ {
		s := <-results
		fmt.Println(s)
	}

}
