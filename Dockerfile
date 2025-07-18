# Est�gio de Build: Utiliza a imagem do SDK do .NET para compilar a aplica��o
# Vamos usar a vers�o 8.0 do .NET SDK, que � a mais recente LTS (Long Term Support)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Define o diret�rio de trabalho dentro do container (onde os comandos ser�o executados)
# /src ser� o diret�rio raiz do seu reposit�rio
WORKDIR /src

# Copia o ficheiro .csproj e o .sln para o container e restaura as depend�ncias NuGet
# Esta � uma otimiza��o de cache: se o .csproj n�o mudar, o restore n�o � refeito.
# Como o .csproj est� na raiz, copiamos para '.'
COPY ["SIGHR.csproj", "."]
COPY ["SIGHR.sln", "."]

# Restaura as depend�ncias NuGet para o projeto SIGHR.csproj
# Como o projeto est� na raiz do WORKDIR (/src), o comando � direto.
RUN dotnet restore "SIGHR.csproj"

# Copia todo o resto do c�digo fonte da sua aplica��o para o container
COPY . /src

# Publica a aplica��o. O '-c Release' � para a vers�o de produ��o.
# '-o /app/publish' � a pasta de sa�da dentro do container.
# '/p:UseAppHost=false' � frequentemente necess�rio para deploys no Render/Linux.
RUN dotnet publish "SIGHR.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Est�gio Final: Utiliza a imagem de runtime do ASP.NET para executar a aplica��o
# Esta imagem � mais leve que a SDK, pois n�o precisa de ferramentas de build.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# Define o diret�rio de trabalho dentro do container para a app final
WORKDIR /app

# Copia os ficheiros publicados do est�gio de 'build' para o est�gio 'final'
COPY --from=build /app/publish .

# Exp�e a porta que a aplica��o ASP.NET Core vai usar
# O Render espera que a aplica��o ou�a na porta 8080 ou 10000.
# Por padr�o, ASP.NET Core num container pode ouvir na 80.
# Para o Render, 8080 � uma boa aposta se n�o for especificado de outra forma.
ENV ASPNETCORE_URLS=http://+:8080 # Diz � app para ouvir na porta 8080
EXPOSE 8080

# Comando para iniciar a aplica��o quando o container � executado
# Substitua "SIGHR.dll" pelo nome exato do ficheiro .dll da sua aplica��o principal.
# Geralmente, � o nome do seu projeto.
ENTRYPOINT ["dotnet", "SIGHR.dll"] # <<--- LEMBRE-SE DE AJUSTAR "SIGHR.dll" SE O NOME DO SEU PROJETO FOR DIFERENTE