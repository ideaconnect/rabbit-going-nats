#!/bin/bash

# Clean previous coverage data
rm -rf /home/bartosz/idct/rabbit-mono/rabbit-going-nats-tests/TestResults

# Run tests with coverage collection
cd /home/bartosz/idct/rabbit-mono/rabbit-going-nats-tests
dotnet test --collect:"XPlat Code Coverage" --settings:coverlet.runsettings

# Find the coverage.cobertura.xml file
COVERAGE_FILE=$(find TestResults -name "coverage.cobertura.xml" | head -1)

if [ -z "$COVERAGE_FILE" ]; then
    echo "Coverage file not found!"
    exit 1
fi

echo "Coverage file found: $COVERAGE_FILE"

# Generate HTML report
reportgenerator \
    -reports:"$COVERAGE_FILE" \
    -targetdir:"coverage-report" \
    -reporttypes:"Html;Console" \
    -classfilters:"-System.*;-Microsoft.*;-*Tests*;-xunit*;-Moq*" \
    -verbosity:"Info"

echo ""
echo "Coverage report generated in: coverage-report/"
echo "Open coverage-report/index.html to view the detailed report"