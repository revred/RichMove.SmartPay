.PHONY: setup test mutation contract load lint zip

setup:
	@echo "Installing tools..."
	@dotnet tool restore || true

test:
	dotnet test --configuration Release /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

mutation:
	dotnet stryker --config-file tests/RichMove.SmartPay.MutationTests/stryker-config.json

contract:
	docker run --rm --network host schemathesis/schemathesis:stable run http://localhost:8080/swagger/v1/swagger.json --checks all --rate-limit=50

load:
	docker run --rm --network host -v $$PWD/deploy/k6:/scripts grafana/k6 run /scripts/smoke.js

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
