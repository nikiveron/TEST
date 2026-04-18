kafka-1:
  image: confluentinc/cp-kafka:7.5.0
  container_name: kafka-1
  depends_on:
    - zookeeper
  ports:
    - "9092:9092"
  environment:
    KAFKA_BROKER_ID: 1
    KAFKA_ZOOKEEPER_CONNECT: zookeeper:2181
    KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://kafka-1:9092
    KAFKA_LISTENERS: PLAINTEXT://0.0.0.0:9092
    KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 3
  healthcheck:
    test: ["CMD", "kafka-broker-api-versions", "--bootstrap-server", "localhost:9092"]
    interval: 10s
    timeout: 5s
    retries: 10
  networks:
    - linktracker-network


    scrapper:
  build:
    context: .
    dockerfile: src/LinkTracker.Scrapper/Dockerfile
  container_name: linktracker-scrapper
  ports:
    - "8080:8080"
  environment:
    - ASPNETCORE_ENVIRONMENT=Development
    - ASPNETCORE_URLS=http://+:8080
  depends_on:
    kafka-1:
      condition: service_healthy
    kafka-2:
      condition: service_healthy
    kafka-3:
      condition: service_healthy
    postgres:
      condition: service_started
  networks:
    - linktracker-network
