# Simple Feature Test Makefile
# This Makefile handles the setup and running of integration tests for rabbit-going-nats

.PHONY: prepare run clean build copy-binaries setup-containers test-messaging all

# Default target
all: prepare run

# Variables
PROJECT_DIR = ../rabbit-going-nats
OUTPUT_DIR = ./bin
APP_NAME = RabbitGoingNats
DOCKER_DIR = ./docker
NATS_DIR = ./nats

# Prepare target: builds the project and sets up everything needed for testing
prepare: clean build copy-binaries setup-containers

# Clean previous builds and containers
clean:
	@echo "Cleaning up previous builds and containers..."
	-docker container rm -f test-nats 2>/dev/null || true
	-docker container rm -f test-rabbit 2>/dev/null || true
	-sudo rm -rf test-log
	-rm -rf $(OUTPUT_DIR)
	-rm -f appsettings.json

# Build the latest rabbit-going-nats project
build:
	@echo "Building rabbit-going-nats project..."
	cd $(PROJECT_DIR) && dotnet build --configuration Release

# Copy required binaries to simple-feature-test directory
copy-binaries:
	@echo "Copying binaries and configuration files..."
	mkdir -p $(OUTPUT_DIR)
	cp -r $(PROJECT_DIR)/bin/Release/net*/* $(OUTPUT_DIR)/
	cp $(PROJECT_DIR)/appsettings.json.dist ./appsettings.json
	@echo "Binaries and configuration copied successfully"

# Setup containers (NATS and RabbitMQ)
setup-containers:
	@echo "Setting up NATS and RabbitMQ containers..."
	mkdir -p $(DOCKER_DIR)
	mkdir -p test-log
	# Create docker/nats directory and copy config
	mkdir -p $(DOCKER_DIR)/nats
	cp $(NATS_DIR)/nats-server.conf $(DOCKER_DIR)/nats/
	# Start NATS container
	docker run --rm -d --name test-nats \
		-v $(PWD)/$(DOCKER_DIR)/nats:/etc/nats/ \
		-v $(PWD)/test-log/:/tmp/log/ \
		-p 4222:4222 -p 8222:8222 \
		nats --http_port 8222 -l /tmp/log/output.txt -D -V
	# Start RabbitMQ container
	docker run -d --rm --name test-rabbit \
		-e RABBITMQ_DEFAULT_USER=user \
		-e RABBITMQ_DEFAULT_PASS=password \
		-p 5672:5672 \
		rabbitmq:3-management
	@echo "Waiting for containers to start..."
	sleep 10
	# Create test queue in RabbitMQ
	docker exec test-rabbit rabbitmqadmin \
		--user=user --password=password \
		declare queue --vhost=/ name=test durable=true
	@echo "Containers setup completed"

# Run target: starts the application and runs the test
run: start-app test-messaging

# Start the rabbit-going-nats application
start-app:
	@echo "Starting rabbit-going-nats application..."
	cd $(OUTPUT_DIR) && \
	dotnet $(APP_NAME).dll &
	@echo "Application started, waiting for initialization..."
	sleep 5

# Test messaging functionality
test-messaging:
	@echo "Running messaging test..."
	@echo "Sending 15 test messages to RabbitMQ..."
	@for i in $$(seq 1 15); do \
		docker exec test-rabbit rabbitmqadmin \
			--user=user --password=password \
			publish exchange=amq.default routing_key=test payload="helloworld"; \
	done
	@echo "Checking if messages were forwarded to NATS..."
	sleep 2
	@wordcount=$$(sudo cat test-log/output.txt | grep helloworld | wc -l); \
	echo "Found $$wordcount messages in NATS log"; \
	if [ $$wordcount -ne 15 ]; then \
		echo "❌ Test FAILED: Expected 15 messages, found $$wordcount"; \
		exit 1; \
	else \
		echo "✅ Test PASSED: All 15 messages successfully forwarded from RabbitMQ to NATS"; \
	fi

# Stop all containers and clean up
stop:
	@echo "Stopping containers and cleaning up..."
	-docker container rm -f test-nats 2>/dev/null || true
	-docker container rm -f test-rabbit 2>/dev/null || true
	-pkill -f "$(APP_NAME).dll" 2>/dev/null || true
	@echo "Cleanup completed"

# Help target
help:
	@echo "Available targets:"
	@echo "  all         - Run prepare and run (default)"
	@echo "  prepare     - Build project, copy binaries, and setup containers"
	@echo "  run         - Start application and run messaging test"
	@echo "  build       - Build the rabbit-going-nats project"
	@echo "  clean       - Clean containers, logs, and binaries"
	@echo "  stop        - Stop containers and application"
	@echo "  help        - Show this help message"