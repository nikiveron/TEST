  kafka-init:
    image: confluentinc/cp-kafka:7.5.0
    depends_on:
      - kafka-1
    entrypoint: [ "/bin/sh", "-c" ]
    command: >
      "
      kafka-topics --create
      --topic link-updates
      --bootstrap-server kafka-1:9092
      --replication-factor 3
      --partitions 3
      "
    networks:
      - linktracker-network      - linktracker-network

  kafka-3:
    image: confluentinc/cp-kafka:7.5.0
    container_name: kafka-3
    depends_on:
      - zookeeper
    ports:
      - "9094:9094"
    environment:
      KAFKA_BROKER_ID: 3
      KAFKA_ZOOKEEPER_CONNECT: zookeeper:2181
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://kafka-3:9094
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 3
    networks:
      - linktracker-network
