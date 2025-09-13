.PHONY: setup test mutation contract load lint zip docs build contracts help

setup:
	@echo "Installing tools..."
	@dotnet tool restore || true

test:
	dotnet test RichMove.SmartPay.sln --configuration Release /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

mutation:
	dotnet stryker --config-file stryker-config.json

contract:
	docker run --rm --network host schemathesis/schemathesis:stable run http://localhost:8080/swagger/v1/swagger.json --checks all --rate-limit=50

load:
	docker run --rm --network host -v $$PWD/deploy/k6:/scripts grafana/k6 run /scripts/smoke.js

build:
	dotnet build -c Release

lint:
	dotnet format --verify-no-changes
	@command -v spectral >/dev/null && spectral lint ../DOCS/API/SmartPay.OpenAPI.yaml || echo "Spectral not installed - skipping OpenAPI lint"

contracts:
	@echo "Validating API contracts..."
	@ls ../DOCS/API/schemas/*.json >/dev/null 2>&1 && echo "✓ JSON schemas found ($$(ls ../DOCS/API/schemas/*.json | wc -l) files)" || echo "! No schemas yet"
	@ls ../DOCS/API/examples/*.json >/dev/null 2>&1 && echo "✓ Examples found ($$(ls ../DOCS/API/examples/*.json | wc -l) files)" || echo "! No examples yet"

docs:
	@echo "=== Documentation Status ==="
	@echo "API Schemas: $$(find ../DOCS/API/schemas -name '*.json' 2>/dev/null | wc -l) files"
	@echo "Examples: $$(find ../DOCS/API/examples -name '*.json' 2>/dev/null | wc -l) files"
	@echo "Runbooks: $$(find ../DOCS/OPS/Runbooks -name '*.md' 2>/dev/null | wc -l) files"
	@echo "WPS: $$(find ../WPS -name '*.md' 2>/dev/null | wc -l) files"

help:
	@echo "Available targets:"
	@echo "  setup     - Install dotnet tools"
	@echo "  build     - Build solution in Release mode"
	@echo "  test      - Run tests with coverage"
	@echo "  lint      - Format check + OpenAPI validation"
	@echo "  contracts - Validate API contracts"
	@echo "  docs      - Show documentation status"
	@echo "  mutation  - Run mutation testing"
	@echo "  contract  - Run contract testing via Schemathesis"
	@echo "  load      - Run load testing via k6"
	@echo "  zip       - Create deployment archive"

zip:
	python3 - <<'PY'
import zipfile, os
root='.'
with zipfile.ZipFile('RichMove.SmartPay.out.zip','w',zipfile.ZIP_DEFLATED) as z:
  for base,_,files in os.walk(root):
    for fn in files:
      if '.git' in base: continue
      z.write(os.path.join(base,fn))
print('Wrote RichMove.SmartPay.out.zip')
PY
