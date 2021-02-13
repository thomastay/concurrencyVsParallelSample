(ns hackernews-downloader.core
  (:require [clj-http.client :as client]
            [clojure.data.json :as json]
            [clojure.string :as str])
  (:import [java.util.concurrent ArrayBlockingQueue])
  (:gen-class))

; URL options, max of 5s for each URL
(def url-options
  {:socket-timeout 5000 :connection-timeout 5000
   :cookie-policy :standard})

;;;;;;;;;;;;;;;;;;; Hacker News API ;;;;;;;;;;;;;;;;;;;;;;
(def hackernews-api-base
  "https://hacker-news.firebaseio.com/v0/")

(def top-stories-url (str hackernews-api-base "topstories.json"))

(defn make-item-url
  "Given an int item, makes the hacker news URL for that item"
  [item]
  (str hackernews-api-base "item/" item ".json"))

(defn get-json!
  [url]
  (-> url
      (client/get url-options)
      (:body)
      (json/read-str)))

(defn get-top-n-urls!
  [n]
  (let [top-ids (get-json! top-stories-url)
        top-n-ids (map make-item-url (take n top-ids))
        top-n-json (doall (map get-json! top-n-ids))
        top-n-urls (map #(get % "url") top-n-json)]
    top-n-urls))
;;;;;;;;;;;;;;;;;; URL processing ;;;;;;;;;;;;;;;;;;;;

(defn word-count
  [s]
  (-> s
      (str/trim)
      (str/split #"\s+")
      (count)))

(defn word-count-url!
  "Get the number of words in the body of the response
   of the given URL"
  [url]
  (try
    (-> url
        (client/get url-options)
        (:body)
        (word-count)
        (#(str url ": " %)))
    (catch Exception e
      (str "Caught exception: " (.getMessage e) " for url " url))))

(defn -main
  [& args]
  (let [n (Long/parseLong (first args))
        q (ArrayBlockingQueue. n)]
    (doseq [url (get-top-n-urls! n)]
      (future (.add q (word-count-url! url))))
    (doseq [_ (range n)]
      (-> (.take q)
          (println)))
    ;; close the threadpool
    (shutdown-agents)))

(comment
  (json/read-str (:body (client/get top-stories-url)))
  (def top-ids *1)
  (def top-5-ids (map make-item-url (take 5 top-ids)))
  (def top-5-json (doall (map #(-> %
                                   (client/get)
                                   (:body)
                                   (json/read-str)) top-5-ids)))
  (map #(get % "url") top-5-json)
  (get-top-n-urls! 2)
  (make-item-url 10)
  ;(require '[clojure.string :as str])
  (count (re-seq #"\w+" "123 asd  4  "))
  (word-count " 123 asd 4  ")
  (str/split (str/trim " 123 asd 4  ") #"\s+")
  (second (get-top-n-urls! 2))
  (word-count-url! *1)
  (-main 20))
