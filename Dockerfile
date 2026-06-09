FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["MMS.Api/MMS.Api.csproj", "MMS.Api/"]
COPY ["MMS.Application/MMS.Application.csproj", "MMS.Application/"]
COPY ["MMS.Domain/MMS.Domain.csproj", "MMS.Domain/"]
COPY ["MMS.Infrastructure/MMS.Infrastructure.csproj", "MMS.Infrastructure/"]

RUN dotnet restore "MMS.Api/MMS.Api.csproj"

COPY . .
RUN dotnet publish "MMS.Api/MMS.Api.csproj" -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/out .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "MMS.Api.dll"]
