# Imagem da API Intrega (.NET 10). Base Debian (não Alpine): o OR-Tools precisa da glibc.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS compilacao
WORKDIR /codigo
COPY . .
RUN dotnet publish src/Intrega.Api/Intrega.Api.csproj -c Release -o /publicado

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=compilacao /publicado .
ENV ASPNETCORE_URLS=http://+:8080 \
    Armazenamento__PastaLocal=/dados/arquivos
VOLUME /dados
EXPOSE 8080
ENTRYPOINT ["dotnet", "Intrega.Api.dll"]
