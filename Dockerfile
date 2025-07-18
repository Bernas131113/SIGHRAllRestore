# Est�gio de Build: Usa a imagem do SDK do .NET para compilar a aplica��o
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia o .csproj e o .sln e restaura as depend�ncias
COPY ["SIGHR.csproj", "."]
COPY ["SIGHR.sln", "."]
RUN dotnet restore "SIGHR.csproj"

# Copia o c�digo fonte e publica
COPY . /src
RUN dotnet publish "SIGHR.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Est�gio Final: Usa a imagem de runtime do ASP.NET para executar a aplica��o
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Copia os ficheiros publicados do est�gio de build
COPY --from=build /app/publish .

# Configura a porta que a aplica��o ASP.NET Core vai usar
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Comando para iniciar a aplica��o
ENTRYPOINT ["dotnet", "SIGHR.dll"] # Lembre-se de ajustar "SIGHR.dll" se o nome do seu projeto for diferente