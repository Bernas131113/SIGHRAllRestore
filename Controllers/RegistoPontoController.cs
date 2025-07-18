// Controllers/RegistoPontoController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIGHR.Areas.Identity.Data;
using SIGHR.Models;
using SIGHR.Models.ViewModels;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SIGHR.Controllers
{
    [Authorize(Policy = "CollaboratorAccessUI")]
    public class RegistoPontoController : Controller
    {
        private readonly SIGHRContext _context;
        private readonly UserManager<SIGHRUser> _userManager;
        private readonly ILogger<RegistoPontoController> _logger;

        public RegistoPontoController(SIGHRContext context, UserManager<SIGHRUser> userManager, ILogger<RegistoPontoController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        private string? GetCurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        [HttpGet]
        public async Task<IActionResult> GetPontoDoDia()
        {
            var utilizadorId = GetCurrentUserId();
            if (string.IsNullOrEmpty(utilizadorId)) return Unauthorized(new { message = "Utilizador não autenticado." });

            var hojeUtc = DateTime.UtcNow.Date; // A data de hoje, já em UTC.

            var registoDoDia = await _context.Horarios
                .Where(r => r.UtilizadorId == utilizadorId && r.Data.Date == hojeUtc)
                .Select(r => new
                {
                    HoraEntrada = r.HoraEntrada.ToString("o"),
                    SaidaAlmoco = r.SaidaAlmoco.ToString("o"),
                    EntradaAlmoco = r.EntradaAlmoco.ToString("o"),
                    HoraSaida = r.HoraSaida.ToString("o")
                })
                .FirstOrDefaultAsync();

            if (registoDoDia == null)
            {
                return Ok(new
                {
                    HoraEntrada = DateTime.MinValue.ToUniversalTime().ToString("o"),
                    SaidaAlmoco = DateTime.MinValue.ToUniversalTime().ToString("o"),
                    EntradaAlmoco = DateTime.MinValue.ToUniversalTime().ToString("o"),
                    HoraSaida = DateTime.MinValue.ToUniversalTime().ToString("o")
                });
            }
            return Ok(registoDoDia);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistarEntrada()
        {
            var utilizadorId = GetCurrentUserId();
            if (string.IsNullOrEmpty(utilizadorId)) return Unauthorized(new { success = false, message = "Utilizador não autenticado." });

            var hojeUtc = DateTime.UtcNow.Date;
            var registoExistente = await _context.Horarios.FirstOrDefaultAsync(r => r.UtilizadorId == utilizadorId && r.Data.Date == hojeUtc);

            if (registoExistente != null)
            {
                if (registoExistente.HoraEntrada == DateTime.MinValue.ToUniversalTime())
                {
                    registoExistente.HoraEntrada = DateTime.UtcNow;
                    _context.Horarios.Update(registoExistente);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("RegistarEntrada: Entrada atualizada para o utilizador {UserId} às {HoraEntrada}.", utilizadorId, registoExistente.HoraEntrada);
                    return Ok(new { success = true, message = "Entrada registada com sucesso!", hora = registoExistente.HoraEntrada.ToString("o") });
                }
                _logger.LogWarning("RegistarEntrada: Utilizador {UserId} já registou a entrada hoje.", utilizadorId);
                return BadRequest(new { success = false, message = "Já registou a entrada para hoje." });
            }

            var novoRegisto = new Horario
            {
                UtilizadorId = utilizadorId,
                Data = hojeUtc,
                HoraEntrada = DateTime.UtcNow,
                HoraSaida = DateTime.MinValue.ToUniversalTime(),
                EntradaAlmoco = DateTime.MinValue.ToUniversalTime(),
                SaidaAlmoco = DateTime.MinValue.ToUniversalTime()
            };

            _context.Horarios.Add(novoRegisto);
            await _context.SaveChangesAsync();
            _logger.LogInformation("RegistarEntrada: Nova entrada registada para o utilizador {UserId} às {HoraEntrada}.", utilizadorId, novoRegisto.HoraEntrada);
            return Ok(new { success = true, message = "Entrada registada com sucesso!", hora = novoRegisto.HoraEntrada.ToString("o") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistarSaidaAlmoco()
        {
            var utilizadorId = GetCurrentUserId();
            if (string.IsNullOrEmpty(utilizadorId)) return Unauthorized(new { success = false, message = "Utilizador não autenticado." });

            var hojeUtc = DateTime.UtcNow.Date;
            var registo = await _context.Horarios.FirstOrDefaultAsync(r => r.UtilizadorId == utilizadorId && r.Data.Date == hojeUtc);

            if (registo == null || registo.HoraEntrada == DateTime.MinValue.ToUniversalTime()) return BadRequest(new { success = false, message = "Registe a entrada do dia primeiro." });
            if (registo.SaidaAlmoco != DateTime.MinValue.ToUniversalTime()) return BadRequest(new { success = false, message = "Já registou a saída para almoço hoje." });
            if (registo.HoraSaida != DateTime.MinValue.ToUniversalTime()) return BadRequest(new { success = false, message = "Já registou a saída do dia." });

            registo.SaidaAlmoco = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("RegistarSaidaAlmoco: Saída para almoço para {UserId} às {SaidaAlmoco}.", utilizadorId, registo.SaidaAlmoco);
            return Ok(new { success = true, message = "Saída para almoço registada!", hora = registo.SaidaAlmoco.ToString("o") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistarEntradaAlmoco()
        {
            var utilizadorId = GetCurrentUserId();
            if (string.IsNullOrEmpty(utilizadorId)) return Unauthorized(new { success = false, message = "Utilizador não autenticado." });

            var hojeUtc = DateTime.UtcNow.Date;
            var registo = await _context.Horarios.FirstOrDefaultAsync(r => r.UtilizadorId == utilizadorId && r.Data.Date == hojeUtc);

            if (registo == null || registo.HoraEntrada == DateTime.MinValue.ToUniversalTime()) return BadRequest(new { success = false, message = "Registo de entrada não encontrado." });
            if (registo.SaidaAlmoco == DateTime.MinValue.ToUniversalTime()) return BadRequest(new { success = false, message = "Registe a saída para almoço primeiro." });
            if (registo.EntradaAlmoco != DateTime.MinValue.ToUniversalTime()) return BadRequest(new { success = false, message = "Já registou a entrada do almoço." });
            if (registo.HoraSaida != DateTime.MinValue.ToUniversalTime()) return BadRequest(new { success = false, message = "Já registou a saída do dia." });

            registo.EntradaAlmoco = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("RegistarEntradaAlmoco: Entrada do almoço para {UserId} às {EntradaAlmoco}.", utilizadorId, registo.EntradaAlmoco);
            return Ok(new { success = true, message = "Entrada do almoço registada!", hora = registo.EntradaAlmoco.ToString("o") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistarSaida()
        {
            var utilizadorId = GetCurrentUserId();
            if (string.IsNullOrEmpty(utilizadorId)) return Unauthorized(new { success = false, message = "Utilizador não autenticado." });

            var hojeUtc = DateTime.UtcNow.Date;
            var registo = await _context.Horarios.FirstOrDefaultAsync(r => r.UtilizadorId == utilizadorId && r.Data.Date == hojeUtc && r.HoraEntrada != DateTime.MinValue.ToUniversalTime() && r.HoraSaida == DateTime.MinValue.ToUniversalTime());

            if (registo == null) return BadRequest(new { success = false, message = "Registo de entrada não encontrado ou saída já efetuada." });
            if (registo.SaidaAlmoco != DateTime.MinValue.ToUniversalTime() && registo.EntradaAlmoco == DateTime.MinValue.ToUniversalTime()) return BadRequest(new { success = false, message = "Registe a entrada do almoço primeiro." });

            registo.HoraSaida = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("RegistarSaida: Saída do dia para {UserId} às {HoraSaida}.", utilizadorId, registo.HoraSaida);
            return Ok(new { success = true, message = "Saída registada com sucesso!", hora = registo.HoraSaida.ToString("o") });
        }

        [HttpGet]
        public async Task<IActionResult> MeuRegisto(DateTime? filtroData)
        {
            ViewData["Title"] = "O Meu Registo de Ponto";
            var userId = GetCurrentUserId();

            if (string.IsNullOrEmpty(userId)) return Unauthorized("Utilizador não autenticado.");

            IQueryable<Horario> query = _context.Horarios.Where(h => h.UtilizadorId == userId);

            if (filtroData.HasValue)
            {
                var filtroDataUtc = filtroData.Value.ToUniversalTime();
                query = query.Where(h => h.Data.Date == filtroDataUtc.Date);
            }

            var horariosDoUsuario = await query.OrderByDescending(h => h.Data).ThenBy(h => h.HoraEntrada).ToListAsync();

            var viewModels = horariosDoUsuario.Select(h =>
            {
                TimeSpan totalTrabalhado = TimeSpan.Zero;
                TimeSpan tempoAlmoco = TimeSpan.Zero;

                TimeSpan horaEntradaTs = h.HoraEntrada.TimeOfDay;
                TimeSpan saidaAlmocoTs = h.SaidaAlmoco.TimeOfDay;
                TimeSpan entradaAlmocoTs = h.EntradaAlmoco.TimeOfDay;
                TimeSpan horaSaidaTs = h.HoraSaida.TimeOfDay;

                if (entradaAlmocoTs > saidaAlmocoTs) tempoAlmoco = entradaAlmocoTs - saidaAlmocoTs;
                if (horaSaidaTs > horaEntradaTs) totalTrabalhado = (horaSaidaTs - horaEntradaTs) - tempoAlmoco;
                if (totalTrabalhado < TimeSpan.Zero) totalTrabalhado = TimeSpan.Zero;

                return new HorarioColaboradorViewModel
                {
                    HorarioId = h.Id,
                    Data = h.Data,
                    HoraEntrada = h.HoraEntrada,
                    SaidaAlmoco = h.SaidaAlmoco,
                    EntradaAlmoco = h.EntradaAlmoco,
                    HoraSaida = h.HoraSaida,
                    TotalHorasTrabalhadas = totalTrabalhado > TimeSpan.Zero ? $"{(int)totalTrabalhado.TotalHours:D2}:{totalTrabalhado.Minutes:D2}" : "--:--"
                };
            }).ToList();

            ViewData["FiltroDataAtual"] = filtroData?.ToString("yyyy-MM-dd");
            return View(viewModels);
        }
    }
}