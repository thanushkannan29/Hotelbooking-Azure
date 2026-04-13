FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["SolHotelBookingAppWebApi/HotelBookingAppWebApi/HotelBookingAppWebApi.csproj", "SolHotelBookingAppWebApi/HotelBookingAppWebApi/"]
RUN dotnet restore "SolHotelBookingAppWebApi/HotelBookingAppWebApi/HotelBookingAppWebApi.csproj"
COPY SolHotelBookingAppWebApi/ SolHotelBookingAppWebApi/
WORKDIR "/src/SolHotelBookingAppWebApi/HotelBookingAppWebApi"
RUN dotnet publish "HotelBookingAppWebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HotelBookingAppWebApi.dll"]
