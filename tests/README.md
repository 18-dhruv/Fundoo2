# NUnit tests

The `Fundoo.UnitTests` project contains unit tests for note lifecycle rules, label-name validation, and password hashing.

Run the suite from the repository root:

```powershell
dotnet test tests/Fundoo.UnitTests/Fundoo.UnitTests.csproj
```

The tests use in-memory fake repositories, so these cases do not require PostgreSQL, Redis, Kafka, SMTP, or running API services.
