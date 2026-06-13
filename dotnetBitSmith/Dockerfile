FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Install curl and Docker CLI client to command child compiler containers
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
RUN curl -fsSL https://download.docker.com/linux/static/stable/x86_64/docker-24.0.7.tgz | tar -xz -C /tmp && \
    mv /tmp/docker/docker /usr/local/bin/docker && \
    rm -rf /tmp/docker

# Build SDK image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["dotnetBitSmith.csproj", "./"]
RUN dotnet restore "./dotnetBitSmith.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "dotnetBitSmith.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "dotnetBitSmith.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final production image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Copy problems.json which is required by ProblemSeeder at startup
COPY problems.json .

ENTRYPOINT ["dotnet", "dotnetBitSmith.dll"]
