# Estágio de Build: Usa a imagem do SDK do .NET para compilar a aplicação
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia o .csproj e o .sln e restaura as dependências
COPY ["SIGHR.csproj", "."]
COPY ["SIGHR.sln", "."]
RUN dotnet restore "SIGHR.csproj"

# Copia o código fonte e publica
COPY . /src
RUN dotnet publish "SIGHR.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Estágio Final: Usa a imagem de runtime do ASP.NET para executar a aplicação
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Copia os ficheiros publicados do estágio de build
COPY --from=build /app/publish .

# Configura a porta que a aplicação ASP.NET Core vai usar
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Comando para iniciar a aplicação
ENTRYPOINT ["dotnet", "SIGHR.dll"] # Lembre-se de ajustar "SIGHR.dll" se o nome do seu projeto for diferente