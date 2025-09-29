#!/bin/bash

# Simple script to run tests with coverage and show a quick summary

echo "🧪 Running tests with code coverage..."

# Run tests with coverage collection (quiet mode)
dotnet test --collect:"XPlat Code Coverage" --results-directory:./TestResults --verbosity:quiet

if [ $? -eq 0 ]; then
    echo "✅ Tests completed successfully"

    # Generate just the text summary for quick viewing
    reportgenerator \
        -reports:"./TestResults/*/coverage.cobertura.xml" \
        -targetdir:"./CoverageReport" \
        -reporttypes:"TextSummary" \
        -verbosity:"Error" \
        -filefilters:"-**/obj/**;-**/bin/**;-**/*SourceGeneration*;-**/*.g.cs" > /dev/null 2>&1

    echo ""
    echo "📊 Code Coverage Summary:"
    echo "=========================="
    cat ./CoverageReport/Summary.txt
    echo ""
    echo "💡 Run './run-coverage.sh' for detailed HTML report"
else
    echo "❌ Tests failed"
    exit 1
fi