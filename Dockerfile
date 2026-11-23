# One Dockerfile for every service. Pick the service with build args, e.g.
#   docker build --build-arg PROJECT=src/Services/User/Fundoo.User.Api/Fundoo.User.Api.csproj \
#                --build-arg DLL=Fundoo.User.Api.dll -t fundoo-user-api .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG PROJECT
WORKDIR /repo
COPY src/ src/
RUN dotnet restore "$PROJECT"
RUN dotnet publish "$PROJECT" -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
ARG DLL
ENV APP_DLL=${DLL} \
    ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production
WORKDIR /app
COPY --from=build /app .
USER app
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_DLL\""]
