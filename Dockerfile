# Estágio de Build: Utiliza a imagem do SDK do .NET para compilar a aplicação
# Vamos usar a versão 8.0 do .NET SDK, que é a mais recente LTS (Long Term Support)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Define o diretório de trabalho dentro do container (onde os comandos serão executados)
# /src será o diretório raiz do seu repositório
WORKDIR /src

# Copia o ficheiro .csproj e o .sln para o container e restaura as dependências NuGet
# Esta é uma otimização de cache: se o .csproj não mudar, o restore não é refeito.
# Como o .csproj está na raiz, copiamos para '.'
COPY ["SIGHR.csproj", "."]
COPY ["SIGHR.sln", "."]

# Restaura as dependências NuGet para o projeto SIGHR.csproj
# Como o projeto está na raiz do WORKDIR (/src), o comando é direto.
RUN dotnet restore "SIGHR.csproj"

# Copia todo o resto do código fonte da sua aplicação para o container
COPY . /src

# Publica a aplicação. O '-c Release' é para a versão de produção.
# '-o /app/publish' é a pasta de saída dentro do container.
# '/p:UseAppHost=false' é frequentemente necessário para deploys no Render/Linux.
RUN dotnet publish "SIGHR.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Estágio Final: Utiliza a imagem de runtime do ASP.NET para executar a aplicação
# Esta imagem é mais leve que a SDK, pois não precisa de ferramentas de build.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# Define o diretório de trabalho dentro do container para a app final
WORKDIR /app

# Copia os ficheiros publicados do estágio de 'build' para o estágio 'final'
COPY --from=build /app/publish .

# Expõe a porta que a aplicação ASP.NET Core vai usar
# O Render espera que a aplicação ouça na porta 8080 ou 10000.
# Por padrão, ASP.NET Core num container pode ouvir na 80.
# Para o Render, 8080 é uma boa aposta se não for especificado de outra forma.
ENV ASPNETCORE_URLS=http://+:8080 # Diz à app para ouvir na porta 8080
EXPOSE 8080

# Comando para iniciar a aplicação quando o container é executado
# Substitua "SIGHR.dll" pelo nome exato do ficheiro .dll da sua aplicação principal.
# Geralmente, é o nome do seu projeto.
ENTRYPOINT ["dotnet", "SIGHR.dll"] # <<--- LEMBRE-SE DE AJUSTAR "SIGHR.dll" SE O NOME DO SEU PROJETO FOR DIFERENTE