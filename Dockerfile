# STAGE01 - Build application and its dependencies
FROM mcr.microsoft.com/dotnet/core/sdk:3.1-alpine AS build
WORKDIR /app
COPY . ./
RUN dotnet restore

# STAGE02 - Publish the application
FROM build AS publish
WORKDIR /app/Net.Bluewalk.RblDnsAggregator
RUN dotnet publish -c Release -o ../out
RUN rm ../out/*.pdb

# STAGE03 - Create the final image
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-alpine AS runtime
LABEL Description="RBL DNS Aggregator"
LABEL Maintainer="Bluewalk"

RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

WORKDIR /app
COPY --from=publish /app/out ./

EXPOSE 53/tcp
EXPOSE 53/udp

CMD ["dotnet", "Net.Bluewalk.RblDnsAggregator.dll"]