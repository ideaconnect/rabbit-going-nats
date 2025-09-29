#!/bin/bash

# Script to run tests with code coverage and generate HTML report

echo "🧪 Running tests with code coverage..."

# Clean previous results
rm -rf ./TestResults
rm -rf ./CoverageReport

# Clean build artifacts to avoid source generation issues
dotnet clean ../rabbit-going-nats/ --verbosity quiet

# Run tests with coverage collection
dotnet test --collect:"XPlat Code Coverage" --results-directory:./TestResults

if [ $? -eq 0 ]; then
    echo "✅ Tests completed successfully"

    echo "📊 Generating coverage report..."

    # Generate HTML report
    reportgenerator \
        -reports:"./TestResults/*/coverage.cobertura.xml" \
        -targetdir:"./CoverageReport" \
        -reporttypes:"Html;TextSummary" \
        -verbosity:"Info" \
        -filefilters:"-**/obj/**;-**/bin/**;-**/*SourceGeneration*;-**/*.g.cs"

    if [ $? -eq 0 ]; then
        echo "✅ Coverage report generated successfully"
        echo ""
        echo "📋 Coverage Summary:"
        cat ./CoverageReport/Summary.txt
        echo ""
        echo "🌐 Open the HTML report: ./CoverageReport/index.html"
        echo "📄 View text summary: ./CoverageReport/Summary.txt"
    else
        echo "❌ Failed to generate coverage report"
        exit 1
    fi
else
    echo "❌ Tests failed"
    exit 1
fi