package main

import (
	"encoding/json"
	"fmt"
	"net/http"
	"time"

	"github.com/gin-gonic/gin"
)

type User struct {
	Id        int    `json:"id"`
	Name      string `json:"name"`
	Email     string `json:"email"`
	BirthDate string `json:"birthDate"`
}

type Book struct {
	Id          int    `json:"id"`
	Title       string `json:"title"`
	Author      string `json:"author"`
	Year        int    `json:"year"`
	IsAvailable bool   `json:"isAvailable"`
	AgeLimit    *int   `json:"ageLimit"`
}

func main() {
	r := gin.Default()

	r.GET("/recommend", func(c *gin.Context) {
		userId := c.Query("userId")
		if userId == "" {
			c.JSON(400, gin.H{"error": "userId обязателен"})
			return
		}

		userResp, err := http.Get("http://dotnetcorelibrary:5096/users")
		if err != nil {
			c.JSON(500, gin.H{"error": "Ошибка при получении пользователей"})
			return
		}
		defer userResp.Body.Close()
		var users []User
		if err := json.NewDecoder(userResp.Body).Decode(&users); err != nil {
			c.JSON(500, gin.H{"error": "Ошибка обработки пользователя"})
			return
		}

		var user *User
		for _, u := range users {
			if fmt.Sprintf("%d", u.Id) == userId {
				user = &u
				break
			}
		}
		if user == nil {
			c.JSON(404, gin.H{"error": "Пользователь не найден"})
			return
		}
		birthTime, err := time.Parse("2006-01-02T15:04:05", user.BirthDate)
		if err != nil {
			c.JSON(500, gin.H{"error": "Ошибка обработки даты: " + err.Error()})
			return
		}
		age := calculateAge(birthTime)

		booksResp, err := http.Get("http://dotnetcorelibrary:5096/books")
		if err != nil {
			c.JSON(500, gin.H{"error": "Ошибка при получении книг"})
			return
		}
		defer booksResp.Body.Close()
		var books []Book
		if err := json.NewDecoder(booksResp.Body).Decode(&books); err != nil {
			c.JSON(500, gin.H{"error": "Ошибка обработки книг"})
			return
		}

		var suitableBooks []Book
		for _, book := range books {
			var limit int
			if book.AgeLimit == nil {
				limit = 0
			} else {
				limit = *book.AgeLimit
			}
			if age >= limit {
				suitableBooks = append(suitableBooks, book)
			}
		}
		c.JSON(200, suitableBooks)
	})

	r.Run(":8082")
}

func calculateAge(birthDate time.Time) int {
	now := time.Now()
	age := now.Year() - birthDate.Year()
	if now.Month() < birthDate.Month() ||
		(now.Month() == birthDate.Month() && now.Day() < birthDate.Day()) {
		age--
	}
	return age
}
