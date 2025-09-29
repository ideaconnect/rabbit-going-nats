# Test Organization

This folder contains unit tests organized by layer and functionality:

## Structure

- **Model/**: Tests for model classes and data structures
  - `NatsConnectionTests.cs` - Comprehensive tests for NATS connection configuration
  - `RabbitMqConnectionTests.cs` - Comprehensive tests for RabbitMQ connection configuration

## Future Organization

As the project grows, additional test categories can be added:

- **Service/**: Tests for service layer components
  - `NatsConnectionHandlerTests.cs`
  - `RabbitMqConnectionHandlerTests.cs`
- **Integration/**: End-to-end integration tests
- **Worker/**: Tests for background worker services

## Test Naming Convention

Tests follow the pattern: `[ClassName]Tests.cs` for unit tests of specific classes.

## Coverage

All model classes currently have 100% test coverage with comprehensive scenario testing.