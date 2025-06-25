package main

import (
	"sync"

	"github.com/gin-gonic/gin"
)

type Stats struct {
	TotalBooks     int `json:"totalBooks"`
	BooksOnLoan    int `json:"booksOnLoan"`
	BooksAvailable int `json:"booksAvailable"`
}

var mu sync.Mutex
var stats = Stats{}

func main() {
	r := gin.Default()

	r.GET("/stats", func(c *gin.Context) {
		mu.Lock()
		defer mu.Unlock()
		c.JSON(200, stats)
	})

	r.POST("/stats/update", func(c *gin.Context) {
		var newStats Stats
		if err := c.ShouldBindJSON(&newStats); err != nil {
			c.JSON(400, gin.H{"error": "Invalid stats format"})
			return
		}
		mu.Lock()
		stats = newStats
		mu.Unlock()
		c.JSON(200, stats)
	})

	r.Run(":8081")
}
