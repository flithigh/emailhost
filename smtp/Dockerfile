FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["smtp.csproj", "./"]
RUN dotnet restore "smtp.csproj"

COPY . .
RUN dotnet publish "smtp.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 25 587 465 5000

ENTRYPOINT ["dotnet", "smtp.dll"]
