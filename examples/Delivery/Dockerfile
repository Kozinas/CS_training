# Optional Linux exercise. The mandatory checks do not build/run this container.
# Build context: repository root. For a production release, pin both image digests.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY examples/Directory.Build.props examples/Directory.Build.props
COPY examples/Web/ examples/Web/
COPY examples/ExpenseLedger.Core/ examples/ExpenseLedger.Core/
WORKDIR /src/examples/Web
RUN dotnet restore LedgerWeb/LedgerWeb.csproj --locked-mode
RUN dotnet publish LedgerWeb/LedgerWeb.csproj -c Release --no-restore --self-contained false -p:UseAppHost=false -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out ./
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "LedgerWeb.dll"]
