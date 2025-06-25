FROM golang:1.24.1 as builder
WORKDIR /app
COPY . .
#RUN go build -o app main.go
RUN CGO_ENABLED=0 GOOS=linux GOARCH=amd64 go build -o app main.go

FROM alpine
WORKDIR /app
COPY --from=builder /app/app .
EXPOSE 8082
CMD ["./app"]
