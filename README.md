# FundoMicroservices

## Google OAuth / SSO

The User API supports Google sign-in at `GET /api/auth/google`. Configure a Google Cloud OAuth client of type **Web application**, then set the client credentials with User Secrets (do not commit real credentials):

```powershell
dotnet user-secrets set "Authentication:Google:ClientId" "YOUR_CLIENT_ID" --project src/Services/User/Fundoo.User.Api
dotnet user-secrets set "Authentication:Google:ClientSecret" "YOUR_CLIENT_SECRET" --project src/Services/User/Fundoo.User.Api
```

For the User API HTTPS launch profile, register this **Authorized redirect URI** in Google Cloud Console:

```text
https://localhost:7001/signin-google
```

The API callback then links the verified Google identity and returns a response requiring email OTP verification. Complete that step with `POST /api/auth/2fa/verify`; the Fundoo access token is issued only after verification. The Notification API and its SMTP settings must be working for the OTP step.

Apply the User database migrations before using Google sign-in:

```powershell
dotnet ef database update --project src/Services/User/Fundoo.User.Infrastructure --startup-project src/Services/User/Fundoo.User.Api
```

## Redis for login OTPs

Login OTP challenges, their expiry, and failed-attempt counts are held only in Redis. Start the local Redis service before running the User API:

```powershell
docker compose up -d redis
```

The default development connection is `localhost:6379`; override `Redis:ConnectionString` for other environments. Apply the database migration to drop the old `OtpTokens` table.
