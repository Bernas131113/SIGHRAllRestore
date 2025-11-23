// Services/RenovacaoFeriasService.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using SIGHR.Areas.Identity.Data;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SIGHR.Services
{
    public class RenovacaoFeriasService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RenovacaoFeriasService> _logger;

        public RenovacaoFeriasService(IServiceProvider serviceProvider, ILogger<RenovacaoFeriasService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Serviço de Renovação de Férias iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessarRenovacaoFerias();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar renovação de férias.");
                }

                // Espera 24 horas antes de verificar de novo.
                // Se o site reiniciar (pelo UptimeRobot ou deploy), ele verifica logo ao arranque.
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }

        private async Task ProcessarRenovacaoFerias()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<SIGHRContext>();

                int anoAtual = DateTime.UtcNow.Year;

                // Procura utilizadores ativos que AINDA NÃO receberam férias deste ano
                var utilizadoresParaAtualizar = await context.Users
                    .Where(u => u.IsActiveEmployee && u.AnoUltimoCreditoFerias < anoAtual)
                    .ToListAsync();

                if (utilizadoresParaAtualizar.Any())
                {
                    _logger.LogInformation($"A processar férias de ano novo para {utilizadoresParaAtualizar.Count} colaboradores...");

                    foreach (var user in utilizadoresParaAtualizar)
                    {
                        // Adiciona os 24 dias
                        user.DiasFeriasDisponiveis += 22;

                        // Marca que este utilizador já recebeu as férias de 'anoAtual'
                        user.AnoUltimoCreditoFerias = anoAtual;
                    }

                    await context.SaveChangesAsync();
                    _logger.LogInformation("Férias atribuídas com sucesso!");
                }
            }
        }
    }
}