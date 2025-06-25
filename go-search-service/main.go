package main

import (
	"encoding/json"
	"io/ioutil"
	"net/http"
	"strings"

	"github.com/gin-gonic/gin"
)

type Book struct {
	ID          int    `json:"id"`
	Title       string `json:"title"`
	Author      string `json:"author"`
	Year        int    `json:"year"`
	IsAvailable bool   `json:"isAvailable"`
}

func main() {
	r := gin.Default()

	r.GET("/books/search", func(c *gin.Context) {
		title := c.Query("title")
		author := c.Query("author")

		resp, err := http.Get("http://dotnet-library/books")
		if err != nil {
			c.JSON(500, gin.H{"error": "Не удалось получить список книг из основного сервиса"})
			return
		}
		defer resp.Body.Close()

		body, _ := ioutil.ReadAll(resp.Body)
		var books []Book
		if err := json.Unmarshal(body, &books); err != nil {
			c.JSON(500, gin.H{"error": "Ошибка разбора JSON"})
			return
		}

		var result []Book
		for _, b := range books {
			matchTitle := title == "" || containsIgnoreCase(b.Title, title)
			matchAuthor := author == "" || containsIgnoreCase(b.Author, author)
			if matchTitle && matchAuthor {
				result = append(result, b)
			}
		}
		c.JSON(200, result)
	})

	r.Run(":8080")
}

func containsIgnoreCase(str, substr string) bool {
	return len(substr) == 0 || (len(str) >= len(substr) &&
		strings.Contains(strings.ToLower(str), strings.ToLower(substr)))
}
