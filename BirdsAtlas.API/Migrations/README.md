# Migrations

Run the following commands to create and apply the initial database migration:

```bash
cd BirdsAtlas.API
dotnet ef migrations add InitialCreate
dotnet ef database update
```

Make sure `appsettings.json` has the correct `DefaultConnection` string before running.
