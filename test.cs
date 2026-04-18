  kafka-ui:
    image: provectuslabs/kafka-ui:latest
    container_name: kafka-ui
    ports:
      - "8090:8080"
    depends_on:
      kafka-1:
        condition: service_healthy
      kafka-2:
        condition: service_healthy
      kafka-3:
        condition: service_healthy
    environment:
      KAFKA_CLUSTERS_0_NAME: linktracker-cluster
      KAFKA_CLUSTERS_0_BOOTSTRAPSERVERS: kafka-1:9092,kafka-2:9093,kafka-3:9094
    networks:
      - linktracker-network      condition: service_healthy
    kafka-3:
      condition: service_healthy
    postgres:
      condition: service_started
  networks:
    - linktracker-network
