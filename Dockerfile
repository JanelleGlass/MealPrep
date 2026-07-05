FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY MealPrep/MealPrep.csproj MealPrep/
RUN dotnet restore MealPrep/MealPrep.csproj
COPY MealPrep/ MealPrep/
RUN dotnet publish MealPrep/MealPrep.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENV MEALPREP_DATA_DIR=/data
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "MealPrep.dll"]
