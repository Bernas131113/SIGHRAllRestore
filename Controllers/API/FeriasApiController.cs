using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIGHR.Areas.Identity.Data;
using SIGHR.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SIGHR.Controllers.Api
{
    // O Request Model continua a usar string para as datas, o que é a abordagem correta.
    public class MarcarFeriasRequest
    {
        public string Start { get; set; } = string.Empty;
        public string End { get; set; } = string.Empty;
    }

    [Route("api/ferias")]
    [ApiController]
    [Authorize(Policy = "CollaboratorAccessUI")]
    public class FeriasApiController : ControllerBase
    {
        private readonly SIGHRContext _context;
        private readonly UserManager<SIGHRUser> _userManager;
        private readonly ILogger<FeriasApiController> _logger;

        public FeriasApiController(SIGHRContext context, UserManager<SIGHRUser> userManager, ILogger<FeriasApiController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet("eventos")]
        public async Task<IActionResult> GetEventos(DateTime start, DateTime end)
        {
            try
            {
                var eventos = await _context.Ferias
                    .Where(f => f.DataInicio < end && f.DataFim >= start)
                    .Include(f => f.User)
                    .Select(f => new {
                        title = f.User != null ? f.User.NomeCompleto ?? f.User.UserName : "Desconhecido",
                        start = f.DataInicio.ToString("yyyy-MM-dd"),
                        end = f.DataFim.AddDays(1).ToString("yyyy-MM-dd"),
                        color = f.Tipo == "Empresa" ? "#dc3545" : "#0d6efd"
                    })
                    .ToListAsync();

                return Ok(eventos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro ao carregar os eventos do calendário de férias.");
                return StatusCode(500, new { message = "Ocorreu um erro interno no servidor ao carregar os eventos." });
            }
        }

        [HttpGet("diasrestantes")]
        public async Task<IActionResult> GetDiasRestantes()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Unauthorized();
                return Ok(new { dias = user.DiasFeriasDisponiveis });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro ao obter os dias de férias restantes.");
                return StatusCode(500, new { message = "Ocorreu um erro ao obter os dias de férias." });
            }
        }

        [HttpPost("marcar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarFerias([FromBody] MarcarFeriasRequest request)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Unauthorized();

                if (!DateTime.TryParse(request.Start, out var dataInicio) || !DateTime.TryParse(request.End, out var dataFim))
                    return BadRequest(new { message = "Formato de data inválido." });

                dataInicio = DateTime.SpecifyKind(dataInicio, DateTimeKind.Utc);
                dataFim = DateTime.SpecifyKind(dataFim, DateTimeKind.Utc);

                int diasUteis = CalcularDiasUteis(dataInicio, dataFim);
                if (diasUteis <= 0) return BadRequest(new { message = "Selecione pelo menos um dia útil." });

                if (user.DiasFeriasDisponiveis < diasUteis)
                    return BadRequest(new { message = $"Não tem dias de férias suficientes. Precisa de {diasUteis}, mas só tem {user.DiasFeriasDisponiveis}." });

                user.DiasFeriasDisponiveis -= diasUteis;

                var novaFerias = new Ferias
                {
                    UtilizadorId = user.Id,
                    DataInicio = dataInicio.Date,
                    DataFim = dataFim.Date,
                    DiasGastos = diasUteis,
                    Tipo = "Individual"
                };

                _context.Ferias.Add(novaFerias);
                await _context.SaveChangesAsync();

                return Ok(new { message = $"Férias marcadas com sucesso! Foram descontados {diasUteis} dias.", diasRestantes = user.DiasFeriasDisponiveis });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERRO CRÍTICO INESPERADO no método MarcarFerias.");
                return StatusCode(500, new { message = "Ocorreu um erro inesperado no servidor. Consulte os logs." });
            }
        }

        [HttpPost("admin/marcarempresa")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminAccessUI")]
        public async Task<IActionResult> MarcarFeriasEmpresa([FromBody] MarcarFeriasRequest request)
        {
            if (!DateTime.TryParse(request.Start, out var dataInicio) || !DateTime.TryParse(request.End, out var dataFim))
                return BadRequest(new { message = "Formato de data inválido." });

            dataInicio = DateTime.SpecifyKind(dataInicio, DateTimeKind.Utc);
            dataFim = DateTime.SpecifyKind(dataFim, DateTimeKind.Utc);

            int diasUteis = CalcularDiasUteis(dataInicio, dataFim);
            if (diasUteis <= 0) return BadRequest(new { message = "Selecione pelo menos um dia útil." });

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // ALTERAÇÃO: A consulta agora filtra apenas por funcionários ativos.
                var funcionariosAtivos = await _userManager.Users
                    .Where(u => u.IsActiveEmployee)
                    .ToListAsync();

                foreach (var user in funcionariosAtivos)
                {
                    user.DiasFeriasDisponiveis -= diasUteis;
                    _context.Ferias.Add(new Ferias
                    {
                        UtilizadorId = user.Id,
                        DataInicio = dataInicio.Date,
                        DataFim = dataFim.Date,
                        DiasGastos = diasUteis,
                        Tipo = "Empresa"
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = $"Férias de empresa marcadas com sucesso para {funcionariosAtivos.Count} funcionários." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha na transação ao marcar férias de empresa.");
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Ocorreu um erro. Nenhuma férias foi marcada." });
            }
        }

        private int CalcularDiasUteis(DateTime start, DateTime end)
        {
            int dias = 0;
            for (DateTime date = start; date <= end; date = date.AddDays(1))
            {
                if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                    dias++;
            }
            return dias;
        }
    }
}