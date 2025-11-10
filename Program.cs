using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SIGHR.Areas.Identity.Data;
using SIGHR.Models;
using SIGHR.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.StaticFiles;
using Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime;
using NodaTime;
using System.Linq;
using Microsoft.AspNetCore.DataProtection;


var builder = WebApplication.CreateBuilder(args);

//
// Bloco 1: Configuração da Base de Dados
//
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("A connection string 'DefaultConnection' não foi encontrada.");
builder.Services.AddDbContext<SIGHRContext>(options => options.UseNpgsql(connectionString));



builder.Services.AddDataProtection()
    .PersistKeysToDbContext<SIGHRContext>();
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
       var jwtKeyString = jwtSettings["Key"] ?? throw new InvalidOperationException("A Chave JWT (Jwt:Key) não foi encontrada.");
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
// Bloco 4: Configuração da Autorização (COM A LÓGICA CORRETA RESTAURADA)
//
builder.Services.AddAuthorization(options => {
    var cookieSchemes = new[] { IdentityConstants.ApplicationScheme, "AdminLoginScheme", "CollaboratorLoginScheme" };
    var jwtScheme = new[] { JwtBearerDefaults.AuthenticationScheme };

    options.AddPolicy("AdminAccessUI", p => p.RequireRole("Admin").AddAuthenticationSchemes(cookieSchemes).RequireAuthenticatedUser());
    
    // CORREÇÃO: A política de Colaborador volta a permitir o acesso a Admins E Colaboradores.
    options.AddPolicy("CollaboratorAccessUI", p => p.RequireRole("Admin", "Collaborator").AddAuthenticationSchemes(cookieSchemes).RequireAuthenticatedUser());
    
    options.AddPolicy("AdminAccessApi", p => p.RequireRole("Admin").AddAuthenticationSchemes(jwtScheme).RequireAuthenticatedUser());
    options.AddPolicy("CollaboratorAccessApi", p => p.RequireRole("Admin", "Collaborator").AddAuthenticationSchemes(jwtScheme).RequireAuthenticatedUser());
    options.AddPolicy("AdminGeneralApiAccess", p => p.RequireRole("Admin").AddAuthenticationSchemes(cookieSchemes).AddAuthenticationSchemes(jwtScheme).RequireAuthenticatedUser());
});

//
// Bloco 5: Configuração de Outros Serviços
//
builder.Services.AddControllersWithViews().AddJsonOptions(options => options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
builder.Services.AddAntiforgery(options => { options.HeaderName = "RequestVerificationToken"; });
builder.Services.AddRazorPages();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddEndpointsApiExplorer();
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
app.UseStaticFiles(new StaticFileOptions { ServeUnknownFileTypes = true, DefaultContentType = "application/octet-stream" });
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.MapControllers();

//
// Bloco 7: Seeding da Base de Dados
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
        if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
        {
            await SeedRolesAsync(roleManager, logger);
            await SeedAdminUserWithHashedPinAsync(userManager, roleManager, pinHasher, logger, configuration);
            await SeedApiTestUserAsync(userManager, roleManager, logger);
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
// Bloco 8: Métodos de Seeding (COM O ADMIN A RECEBER AS DUAS FUNÇÕES)
//
async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager, ILogger<Program> logger)
{
    string[] roleNames = { "Admin", "Collaborator" };
    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            var result = await roleManager.CreateAsync(new IdentityRole(roleName));
            if (result.Succeeded)
            {
                logger.LogInformation("Função '{RoleName}' criada.", roleName);
            }
            else
            {
                foreach (var error in result.Errors)
                {
                    logger.LogError("Erro ao criar a função '{RoleName}': {ErrorDescription}", roleName, error.Description);
                }
            }
        }
    }
}

async Task SeedAdminUserWithHashedPinAsync(UserManager<SIGHRUser> userManager, RoleManager<IdentityRole> roleManager, IPasswordHasher<SIGHRUser> pinHasher, ILogger<Program> logger, IConfiguration configuration)
{
    string configUserName = configuration["SeedAdminCredentials:UserName"] ?? "bernardo.alves";
    string configEmail = configuration["SeedAdminCredentials:Email"] ?? $"admin_{configUserName}@sighr.com";
    if (!int.TryParse(configuration["SeedAdminCredentials:PIN"] ?? "1311", out int adminPIN)) adminPIN = 1311;
    string configNomeCompleto = configuration["SeedAdminCredentials:NomeCompleto"] ?? "Bernardo Alves (Admin)";
    string configPassword = configuration["SeedAdminCredentials:Password"] ?? Guid.NewGuid().ToString() + "P@ss1!";
    
    var existingUser = await userManager.FindByNameAsync(configUserName);
    if (existingUser == null)
    {
        var user = new SIGHRUser { UserName = configUserName, Email = configEmail, EmailConfirmed = true, NomeCompleto = configNomeCompleto, Tipo = "Admin", PinnedHash = pinHasher.HashPassword(null!, adminPIN.ToString()) };
        var result = await userManager.CreateAsync(user, configPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, "Admin");
            await userManager.AddToRoleAsync(user, "Collaborator");
            logger.LogInformation("Utilizador Admin '{UserName}' criado com as funções 'Admin' e 'Collaborator'.", user.UserName);
        }
        else
        {
            foreach (var error in result.Errors)
            {
                logger.LogError("Erro ao criar utilizador Admin '{UserName}': {ErrorDescription}", user.UserName, error.Description);
            }
        }
    }
    else
    {
        if (!await userManager.IsInRoleAsync(existingUser, "Admin"))
        {
            await userManager.AddToRoleAsync(existingUser, "Admin");
            logger.LogInformation("Função 'Admin' adicionada ao utilizador existente '{UserName}'.", existingUser.UserName);
        }
        if (!await userManager.IsInRoleAsync(existingUser, "Collaborator"))
        {
            await userManager.AddToRoleAsync(existingUser, "Collaborator");
            logger.LogInformation("Função 'Collaborator' adicionada ao utilizador existente '{UserName}'.", existingUser.UserName);
        }
        if (existingUser.Tipo != "Admin")
        {
            existingUser.Tipo = "Admin";
            await userManager.UpdateAsync(existingUser);
        }
    }
}

async Task SeedApiTestUserAsync(UserManager<SIGHRUser> userManager, RoleManager<IdentityRole> roleManager, ILogger<Program> logger)
{
    string testUserName = "otv";
    string testEmail = "otv@test.com";
    string testPassword = "PasswordApi123!";
    string targetRoleName = "Admin";

    var existingUser = await userManager.FindByNameAsync(testUserName);
    if (existingUser == null)
    {
        var apiUser = new SIGHRUser { UserName = testUserName, Email = testEmail, EmailConfirmed = true, NomeCompleto = "Utilizador OTV Teste API", Tipo = targetRoleName, PinnedHash = null };
        var result = await userManager.CreateAsync(apiUser, testPassword);
        if (result.Succeeded)
        {
            if (await roleManager.RoleExistsAsync(targetRoleName))
            {
                await userManager.AddToRoleAsync(apiUser, targetRoleName);
            }
        }
    }
    else
    {
        if (!await userManager.IsInRoleAsync(existingUser, targetRoleName))
        {
            await userManager.AddToRoleAsync(existingUser, targetRoleName);
        }
        if (existingUser.Tipo != targetRoleName)
        {
            existingUser.Tipo = targetRoleName;
            await userManager.UpdateAsync(existingUser);
        }
    }
}