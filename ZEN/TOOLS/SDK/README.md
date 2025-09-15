# SDK Generation (WP6)

Generate clients from the OpenAPI served at `/swagger/v1/swagger.json`.

## TypeScript (openapi-typescript)
```bash
npx openapi-typescript http://localhost:5001/swagger/v1/swagger.json -o ./TOOLS/SDK/types/smartpay.ts
```

## C# (NSwag CLI)
```bash
dotnet tool install --global NSwag.ConsoleCore
nswag openapi2csclient /input:http://localhost:5001/swagger/v1/swagger.json /classname:SmartPayClient /namespace:SmartPay.Sdk /output:./TOOLS/SDK/csharp/SmartPayClient.cs
```

> Run these offline in dev; commit generated clients only on release tags.