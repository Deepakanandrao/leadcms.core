#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/LeadCMS/LeadCMS.csproj", "src/LeadCMS/"]
RUN dotnet restore "src/LeadCMS/LeadCMS.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "LeadCMS.sln" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "./src/LeadCMS/LeadCMS.csproj" -c Release -o /app/publish /p:UseAppHost=false
RUN dotnet publish "./plugins/LeadCMS.Plugin.EmailSync/LeadCMS.Plugin.EmailSync.csproj" -c Release -o /app/publish/plugins/LeadCMS.Plugin.EmailSync /p:UseAppHost=false
RUN dotnet publish "./plugins/LeadCMS.Plugin.ReverseProxy/LeadCMS.Plugin.ReverseProxy.csproj" -c Release -o /app/publish/plugins/LeadCMS.Plugin.ReverseProxy /p:UseAppHost=false
RUN dotnet publish "./plugins/LeadCMS.Plugin.SendGrid/LeadCMS.Plugin.SendGrid.csproj" -c Release -o /app/publish/plugins/LeadCMS.Plugin.SendGrid /p:UseAppHost=false
RUN dotnet publish "./plugins/LeadCMS.Plugin.Sms/LeadCMS.Plugin.Sms.csproj" -c Release -o /app/publish/plugins/LeadCMS.Plugin.Sms /p:UseAppHost=false
RUN dotnet publish "./plugins/LeadCMS.Plugin.Vsto/LeadCMS.Plugin.Vsto.csproj" -c Release -o /app/publish/plugins/LeadCMS.Plugin.Vsto /p:UseAppHost=false
RUN dotnet publish "./plugins/LeadCMS.Plugin.Site/LeadCMS.Plugin.Site.csproj" -c Release -o /app/publish/plugins/LeadCMS.Plugin.Site /p:UseAppHost=false
RUN dotnet publish "./plugins/LeadCMS.Plugin.AI/LeadCMS.Plugin.AI.csproj" -c Release -o /app/publish/plugins/LeadCMS.Plugin.AI /p:UseAppHost=false
RUN dotnet publish "./plugins/LeadCMS.Plugin.Deploy/LeadCMS.Plugin.Deploy.csproj" -c Release -o /app/publish/plugins/LeadCMS.Plugin.Deploy /p:UseAppHost=false

FROM base AS final
RUN apk add --no-cache icu-libs
RUN apk add --no-cache icu-data-full
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app
COPY --from=publish /app/publish .
HEALTHCHECK --interval=1m --timeout=10s --retries=3 CMD wget --spider --tries=1 --no-verbose http://localhost:80/api/version || exit 1
ENTRYPOINT ["dotnet", "LeadCMS.dll"]
