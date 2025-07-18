using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SIGHR.Areas.Identity.Data;
using SIGHR.Models; // Necessário para a classe Material e outras entidades no seeding
using SIGHR.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Linq; // Necessário para .Linq

var builder = WebApplication.CreateBuilder(args);

//
// Bloco 1: Configuração da Base de Dados
//
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("A connection string 'DefaultConnection' não foi encontrada. Configure-a em appsettings.json, User Secrets, ou variáveis de ambiente.");

builder.Services.AddDbContext<SIGHRContext>(options =>
    options.UseNpgsql(connectionString)); // Usar Npgsql para PostgreSQL

//
// Bloco 2: Configuração do ASP.NET Core Identity
//
builder.Services.AddDefaultIdentity<SIGHRUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<SIGHRContext>();

//
// Bloco 3: Configuração da Autenticação
//
builder.Services.AddAuthentication()
   .AddCookie("AdminLoginScheme", options =>
   {
       options.LoginPath = "/Identity/Account/AdminLogin";
       options.AccessDeniedPath = "/Identity/Account/AccessDenied";
       options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
   })
   .AddCookie("CollaboratorLoginScheme", options =>
   {
       options.LoginPath = "/Identity/Account/CollaboratorPinLogin";
       options.AccessDeniedPath = "/Identity/Account/AccessDenied";
       options.ExpireTimeSpan = TimeSpan.FromHours(8);
   })
   .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
   {
       var jwtSettings = builder.Configuration.GetSection("Jwt");
       var jwtKeyString = jwtSettings["Key"] ?? throw new InvalidOperationException("A Chave JWT (Jwt:Key) não foi encontrada. Configure-a.");
       var key = Encoding.UTF8.GetBytes(jwtKeyString);
       options.TokenValidationParameters = new TokenValidationParameters
       {
           ValidateIssuer = true,
           ValidateAudience = true,
           ValidateLifetime = true,
           ValidateIssuerSigningKey = true,
           ValidIssuer = jwtSettings["Issuer"],
           ValidAudience = jwtSettings["Audience"],
           IssuerSigningKey = new SymmetricSecurityKey(key),
           ClockSkew = TimeSpan.Zero
       };
   });

//
// Bloco 4: Configuração da Autorização
//
builder.Services.AddAuthorization(options => {
    var cookieSchemes = new[] { IdentityConstants.ApplicationScheme, "AdminLoginScheme", "CollaboratorLoginScheme" };
    var jwtScheme = new[] { JwtBearerDefaults.AuthenticationScheme };

    options.AddPolicy("AdminAccessUI", p => p.RequireRole("Admin").AddAuthenticationSchemes(cookieSchemes).RequireAuthenticatedUser());
    options.AddPolicy("CollaboratorAccessUI", p => p.RequireRole("Admin", "Collaborator").AddAuthenticationSchemes(cookieSchemes).RequireAuthenticatedUser());
    options.AddPolicy("AdminAccessApi", p => p.RequireRole("Admin").AddAuthenticationSchemes(jwtScheme).RequireAuthenticatedUser());
    options.AddPolicy("CollaboratorAccessApi", p => p.RequireRole("Admin", "Collaborator").AddAuthenticationSchemes(jwtScheme).RequireAuthenticatedUser());
    options.AddPolicy("AdminGeneralApiAccess", p => p.RequireRole("Admin").AddAuthenticationSchemes(cookieSchemes).AddAuthenticationSchemes(jwtScheme).RequireAuthenticatedUser());
});

//
// Bloco 5: Configuração de Outros Serviços da Aplicação
//
builder.Services.AddControllersWithViews().AddJsonOptions(options => options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
builder.Services.AddRazorPages();
builder.Services.AddScoped<TokenService>();
builder.Services.AddEndpointsApiExplorer();

// Configuração do Swagger para documentação da API.
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "API do SIGHR",
        Version = "v1",
        Description = "API para o Sistema Integrado de Gestão de Recursos Humanos"
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

//
// Bloco 6: Pipeline de Middleware HTTP
//
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.MapControllers();

//
// Bloco 7: Seeding da Base de Dados (Criação de Dados Iniciais)
//
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var userManager = services.GetRequiredService<UserManager<SIGHRUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = services.GetRequiredService<ILogger<Program>>();
        var configuration = services.GetRequiredService<IConfiguration>();
        var pinHasher = services.GetRequiredService<IPasswordHasher<SIGHRUser>>();
        var context = services.GetRequiredService<SIGHRContext>();

        // Executar seeding APENAS em desenvolvimento ou ambiente de teste
        if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
        {
            await SeedRolesAsync(roleManager, logger);
            await SeedAdminUserWithHashedPinAsync(userManager, roleManager, pinHasher, logger, configuration);
            await SeedApiTestUserAsync(userManager, roleManager, logger); // Alterado para otv
            
        
        }
        else
        {
            logger.LogInformation("Seeding de dados ignorado em ambiente de produção.");
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocorreu um erro durante o seeding da base de dados.");
    }
}
app.Run();


//
// Bloco 8: Métodos de Seeding
//

// <summary>
// Cria as funções (Roles) "Admin" e "Collaborator" se ainda não existirem.
// </summary>
async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager, ILogger<Program> logger)
{
    string[] roleNames = { "Admin", "Collaborator" };
    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            var result = await roleManager.CreateAsync(new IdentityRole(roleName));
            if (result.Succeeded) logger.LogInformation("Função '{RoleName}' criada.", roleName);
            else foreach (var error in result.Errors) logger.LogError("Erro ao criar a função '{RoleName}': {ErrorDescription}", roleName, error.Description);
        }
    }
}

// <summary>
// Cria um utilizador administrador inicial com um PIN codificado, lendo os dados de appsettings.json.
// Garante que o utilizador tem a Role 'Admin' mesmo que já exista.
// </summary>
async Task SeedAdminUserWithHashedPinAsync(UserManager<SIGHRUser> userManager, RoleManager<IdentityRole> roleManager, IPasswordHasher<SIGHRUser> pinHasher, ILogger<Program> logger, IConfiguration configuration)
{
    // Credenciais para o utilizador Admin. Em produção, DEVE usar variáveis de ambiente ou Key Vault.
    string configUserName = configuration["SeedAdminCredentials:UserName"] ?? "bernardo.alves";
    string configEmail = configuration["SeedAdminCredentials:Email"] ?? $"admin_{configUserName}@sighr.com";
    if (!int.TryParse(configuration["SeedAdminCredentials:PIN"] ?? "1311", out int adminPIN)) adminPIN = 1311;
    string configNomeCompleto = configuration["SeedAdminCredentials:NomeCompleto"] ?? "Bernardo Alves (Admin)";
    string configPassword = configuration["SeedAdminCredentials:Password"] ?? Guid.NewGuid().ToString() + "P@ss1!"; // Use uma password forte e aleatória em produção.

    string adminRoleName = "Admin";
    var existingUser = await userManager.FindByNameAsync(configUserName);

    if (existingUser == null)
    {
        var user = new SIGHRUser { UserName = configUserName, Email = configEmail, EmailConfirmed = true, NomeCompleto = configNomeCompleto, Tipo = adminRoleName, PinnedHash = pinHasher.HashPassword(null!, adminPIN.ToString()) };
        var result = await userManager.CreateAsync(user, configPassword);
        if (result.Succeeded) await userManager.AddToRoleAsync(user, adminRoleName);
        else foreach (var error in result.Errors) logger.LogError("Erro ao criar utilizador Admin '{UserName}': {ErrorDescription}", user.UserName, error.Description);
    }
    else
    {
        // Garante que o utilizador já existente tem a role 'Admin'
        if (!await userManager.IsInRoleAsync(existingUser, adminRoleName))
        {
            await userManager.AddToRoleAsync(existingUser, adminRoleName);
            logger.LogInformation("Utilizador Admin '{UserName}' adicionado à função '{RoleName}'.", existingUser.UserName, adminRoleName);
        }
        // Garante que a propriedade Tipo também está correta
        if (existingUser.Tipo != adminRoleName)
        {
            existingUser.Tipo = adminRoleName;
            await userManager.UpdateAsync(existingUser);
            logger.LogInformation("Utilizador Admin '{UserName}' Tipo atualizado para '{NewType}'.", existingUser.UserName, adminRoleName);
        }
    }
}

// <summary>
// Cria um utilizador de teste específico para a API, com uma palavra-passe conhecida.
// Garante que o utilizador tem a Role 'Admin' (ou a role pretendida) e o Tipo corretos, mesmo que já exista.
// </summary>
async Task SeedApiTestUserAsync(UserManager<SIGHRUser> userManager, RoleManager<IdentityRole> roleManager, ILogger<Program> logger)
{
    string testUserName = "otv"; // Nome de utilizador para o teste da API
    string testEmail = "otv@test.com";
    string testPassword = "PasswordApi123!"; // Palavra-passe do utilizador de teste
    string targetRoleName = "Admin"; // A função (Role) que queremos que este utilizador tenha

    var existingUser = await userManager.FindByNameAsync(testUserName);
    if (existingUser == null)
    {
        var apiUser = new SIGHRUser
        {
            UserName = testUserName,
            Email = testEmail,
            EmailConfirmed = true,
            NomeCompleto = "Utilizador OTV Teste API",
            Tipo = targetRoleName,
            PinnedHash = null
        };

        var result = await userManager.CreateAsync(apiUser, testPassword);
        if (result.Succeeded)
        {
            logger.LogInformation("Utilizador de teste de API '{UserName}' criado com sucesso.", apiUser.UserName);
            if (await roleManager.RoleExistsAsync(targetRoleName))
            {
                await userManager.AddToRoleAsync(apiUser, targetRoleName);
                logger.LogInformation("Utilizador '{UserName}' adicionado à função '{RoleName}'.", apiUser.UserName, targetRoleName);
            }
        }
    }
    else
    {
        // Se o utilizador já existe, garante que ele tem a role correta.
        if (!await userManager.IsInRoleAsync(existingUser, targetRoleName))
        {
            await userManager.AddToRoleAsync(existingUser, targetRoleName);
            logger.LogInformation("Utilizador existente '{UserName}' adicionado à função '{RoleName}'.", existingUser.UserName, targetRoleName);
        }
        // E garante que a propriedade Tipo também está correta.
        if (existingUser.Tipo != targetRoleName)
        {
            existingUser.Tipo = targetRoleName;
            await userManager.UpdateAsync(existingUser);
            logger.LogInformation("Utilizador existente '{UserName}' Tipo atualizado para '{NewType}'.", existingUser.UserName, targetRoleName);
        }
    }
}



