# Multi-stage build — pattern from https://learn.microsoft.com/aspnet/core/host-and-deploy/docker/building-net-docker-images
# ASP.NET runtime images listen on port 8080 by default (non-root); see https://learn.microsoft.com/dotnet/core/docker/publish-as-container

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["SEP490-G59-AICommunityStoryWriting.sln", "./"]
COPY ["AIStory.API/AIStory.API.csproj", "AIStory.API/"]
COPY ["BusinessObjects/BusinessObjects.csproj", "BusinessObjects/"]
COPY ["DataAccessObjects/DataAccessObjects.csproj", "DataAccessObjects/"]
COPY ["Repositories/Repositories.csproj", "Repositories/"]
COPY ["Services/Services.csproj", "Services/"]

RUN dotnet restore "AIStory.API/AIStory.API.csproj"

COPY . .
WORKDIR "/src/AIStory.API"
RUN dotnet publish "AIStory.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AIStory.API.dll"]
