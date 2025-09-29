# Code Coverage for RabbitGoingNats Tests

This project now includes comprehensive code coverage reporting for the NatsConnection model tests.

## 🚀 Quick Start

### Run Tests with Coverage Summary
```bash
./coverage-summary.sh
```

### Run Tests with Full HTML Report
```bash
./run-coverage.sh
```

## 📊 Coverage Results

### Current Coverage for NatsConnection Model: **100%** ✅

The `NatsConnection` model has complete test coverage with 23 comprehensive tests covering:

- ✅ Basic property initialization and assignment
- ✅ Default values for optional properties
- ✅ Token-based authentication scenarios
- ✅ Username/password authentication scenarios
- ✅ Mixed authentication configurations
- ✅ Various URL formats (standard, TLS, cluster)
- ✅ Various subject patterns (simple, hierarchical)
- ✅ Property mutability after initialization
- ✅ Null value handling for optional properties

### Overall Project Coverage Summary
- **Line Coverage**: 0.7% (5/643 coverable lines)
- **Branch Coverage**: 0% (0/166 branches)
- **Method Coverage**: 11.3% (5/44 methods)

**Class-by-Class Breakdown:**
- `RabbitGoingNats.Model.NatsConnection`: **100%** ✅
- `RabbitGoingNats.Model.RabbitMqConnection`: 0%
- `RabbitGoingNats.Service.NatsConnectionHandler`: 0%
- `RabbitGoingNats.Service.RabbitMqConnectionHandler`: 0%
- `RabbitGoingNats.Worker`: 0%
- `Program`: 0%

## 📁 Report Files

After running coverage, you'll find:

- `./CoverageReport/index.html` - Interactive HTML report with detailed coverage
- `./CoverageReport/Summary.txt` - Text summary of coverage metrics
- `./TestResults/*/coverage.cobertura.xml` - Raw coverage data (Cobertura format)

## 🌐 Viewing HTML Report

Open the HTML report in your browser:
```bash
# Linux/Mac
xdg-open ./CoverageReport/index.html

# Windows
start ./CoverageReport/index.html

# Or manually open in any browser:
# file:///path/to/rabbit-going-nats-tests/CoverageReport/index.html
```

## 🛠 Manual Commands

If you prefer to run commands manually:

```bash
# Run tests with coverage collection
dotnet test --collect:"XPlat Code Coverage" --results-directory:./TestResults

# Generate HTML report from coverage data
reportgenerator \
    -reports:"./TestResults/*/coverage.cobertura.xml" \
    -targetdir:"./CoverageReport" \
    -reporttypes:"Html;TextSummary"
```

## 📋 Dependencies

The following packages are required for code coverage:
- `coverlet.collector` (6.0.0) - Coverage data collection
- `coverlet.msbuild` (6.0.0) - MSBuild integration
- `dotnet-reportgenerator-globaltool` - HTML report generation

These are already configured in the project.

## 💡 Next Steps

To improve overall project coverage, consider adding tests for:
1. `RabbitMqConnection` model
2. `NatsConnectionHandler` service
3. `RabbitMqConnectionHandler` service
4. `Worker` background service
5. Application startup (`Program.cs`)

The current high coverage of `NatsConnection` (100%) demonstrates the testing methodology that can be applied to other components.