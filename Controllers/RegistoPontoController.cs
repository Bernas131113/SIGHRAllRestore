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
    /// <summary>
    /// Controlador responsável por toda a lógica de registo de ponto.
    /// Inclui os endpoints da API para o dashboard do colaborador e a página de histórico de registos.
    /// </summary>
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

        // Método auxiliar para obter o ID do utilizador autenticado de forma centralizada.
        private string? GetCurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        //
        // Bloco: Endpoints da API para o Dashboard (AJAX)
        // Estes métodos respondem a pedidos JavaScript para registar o ponto em tempo real.
        //

        /// <summary>
        /// API para obter o registo de ponto do dia do utilizador autenticado.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPontoDoDia()
        {
            var utilizadorId = GetCurrentUserId();
            if (string.IsNullOrEmpty(utilizadorId))
            {
                return Unauthorized(new { message = "Utilizador não autenticado." });
            }

            // --- CORREÇÃO: Data de hoje convertida para UTC para comparação com a BD ---
            var hojeUtc = DateTime.Today.ToUniversalTime();
            // ---------------------------------------------------------------------

            var registoDoDia = await _context.Horarios
                .Where(r => r.UtilizadorId == utilizadorId && r.Data.Date == hojeUtc.Date) // Compara com a data em UTC
                .Select(r => new
                {
                    HoraEntrada = r.HoraEntrada.ToString(@"hh\:mm\:ss"),
                    SaidaAlmoco = r.SaidaAlmoco.ToString(@"hh\:mm\:ss"),
                    EntradaAlmoco = r.EntradaAlmoco.ToString(@"hh\:mm\:ss"),
                    HoraSaida = r.HoraSaida.ToString(@"hh\:mm\:ss")
                })
                .FirstOrDefaultAsync();

            if (registoDoDia == null)
            {
                return Ok(new
                {
                    HoraEntrada = TimeSpan.Zero.ToString(@"hh\:mm\:ss"),
                    SaidaAlmoco = TimeSpan.Zero.ToString(@"hh\:mm\:ss"),
                    EntradaAlmoco = TimeSpan.Zero.ToString(@"hh\:mm\:ss"),
                    HoraSaida = TimeSpan.Zero.ToString(@"hh\:mm\:ss")
                });
            }
            return Ok(registoDoDia);
        }

        /// <summary>
        /// API para registar a hora de entrada do utilizador.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistarEntrada()
        {
            var utilizadorId = GetCurrentUserId();
            if (string.IsNullOrEmpty(utilizadorId))
            {
                _logger.LogWarning("RegistarEntrada: Tentativa de acesso não autenticado.");
                return Unauthorized(new { success = false, message = "Utilizador não autenticado." });
            }

            // --- CORREÇÃO: Data de hoje convertida para UTC para comparação e armazenamento ---
            var hojeUtc = DateTime.Today.ToUniversalTime();
            // --------------------------------------------------------------------------------

            var registoExistente = await _context.Horarios
                .FirstOrDefaultAsync(r => r.UtilizadorId == utilizadorId && r.Data.Date == hojeUtc.Date); // Compara com a data em UTC

            if (registoExistente != null)
            {
                if (registoExistente.HoraEntrada == TimeSpan.Zero)
                {
                    registoExistente.HoraEntrada = DateTime.Now.TimeOfDay; // TimeSpan não tem fuso horário, está ok
                    _context.Horarios.Update(registoExistente);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("RegistarEntrada: Entrada atualizada para o utilizador {UserId} às {HoraEntrada}.", utilizadorId, registoExistente.HoraEntrada);
                    return Ok(new { success = true, message = "Entrada registada com sucesso!", hora = registoExistente.HoraEntrada.ToString(@"hh\:mm\:ss") });
                }
                _logger.LogWarning("RegistarEntrada: Utilizador {UserId} já registou a entrada hoje.", utilizadorId);
                return BadRequest(new { success = false, message = "Já registou a entrada para hoje." });
            }

            var novoRegisto = new Horario
            {
                UtilizadorId = utilizadorId,
                Data = hojeUtc.Date, // Guarda a data em UTC
                HoraEntrada = DateTime.Now.TimeOfDay, // TimeSpan está ok
                HoraSaida = TimeSpan.Zero,
                EntradaAlmoco = TimeSpan.Zero,
                SaidaAlmoco = TimeSpan.Zero
            };

            _context.Horarios.Add(novoRegisto);
            await _context.SaveChangesAsync();
            _logger.LogInformation("RegistarEntrada: Nova entrada registada para o utilizador {UserId} às {HoraEntrada}.", utilizadorId, novoRegisto.HoraEntrada);
            return Ok(new { success = true, message = "Entrada registada com sucesso!", hora = novoRegisto.HoraEntrada.ToString(@"hh\:mm\:ss") });
        }

        /// <summary>
        /// API para registar a hora de saída para almoço.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistarSaidaAlmoco()
        {
            var utilizadorId = GetCurrentUserId();
            if (string.IsNullOrEmpty(utilizadorId)) return Unauthorized(new { success = false, message = "Utilizador não autenticado." });

            // --- CORREÇÃO: Data de hoje convertida para UTC para comparação ---
            var hojeUtc = DateTime.Today.ToUniversalTime();
            // ---------------------------------------------------------------

            var registo = await _context.Horarios.FirstOrDefaultAsync(r => r.UtilizadorId == utilizadorId && r.Data.Date == hojeUtc.Date);

            if (registo == null || registo.HoraEntrada == TimeSpan.Zero) return BadRequest(new { success = false, message = "Registe a entrada do dia primeiro." });
            if (registo.SaidaAlmoco != TimeSpan.Zero) return BadRequest(new { success = false, message = "Já registou a saída para almoço hoje." });
            if (registo.HoraSaida != TimeSpan.Zero) return BadRequest(new { success = false, message = "Já registou a saída do dia." });

            registo.SaidaAlmoco = DateTime.Now.TimeOfDay; // TimeSpan está ok
            await _context.SaveChangesAsync();
            _logger.LogInformation("RegistarSaidaAlmoco: Saída para almoço para {UserId} às {SaidaAlmoco}.", utilizadorId, registo.SaidaAlmoco);
            return Ok(new { success = true, message = "Saída para almoço registada!", hora = registo.SaidaAlmoco.ToString(@"hh\:mm\:ss") });
        }

        /// <summary>
        /// API para registar a hora de entrada após o almoço.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistarEntradaAlmoco()
        {
            var utilizadorId = GetCurrentUserId();
            if (string.IsNullOrEmpty(utilizadorId)) return Unauthorized(new { success = false, message = "Utilizador não autenticado." });

            // --- CORREÇÃO: Data de hoje convertida para UTC para comparação ---
            var hojeUtc = DateTime.Today.ToUniversalTime();
            // ---------------------------------------------------------------

            var registo = await _context.Horarios.FirstOrDefaultAsync(r => r.UtilizadorId == utilizadorId && r.Data.Date == hojeUtc.Date);

            if (registo == null || registo.HoraEntrada == TimeSpan.Zero) return BadRequest(new { success = false, message = "Registo de entrada não encontrado." });
            if (registo.SaidaAlmoco == TimeSpan.Zero) return BadRequest(new { success = false, message = "Registe a saída para almoço primeiro." });
            if (registo.EntradaAlmoco != TimeSpan.Zero) return BadRequest(new { success = false, message = "Já registou a entrada do almoço." });
            if (registo.HoraSaida != TimeSpan.Zero) return BadRequest(new { success = false, message = "Já registou a saída do dia." });

            registo.EntradaAlmoco = DateTime.Now.TimeOfDay; // TimeSpan está ok
            await _context.SaveChangesAsync();
            _logger.LogInformation("RegistarEntradaAlmoco: Entrada do almoço para {UserId} às {EntradaAlmoco}.", utilizadorId, registo.EntradaAlmoco);
            return Ok(new { success = true, message = "Entrada do almoço registada!", hora = registo.EntradaAlmoco.ToString(@"hh\:mm\:ss") });
        }

        /// <summary>
        /// API para registar a hora de saída do dia.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistarSaida()
        {
            var utilizadorId = GetCurrentUserId();
            if (string.IsNullOrEmpty(utilizadorId)) return Unauthorized(new { success = false, message = "Utilizador não autenticado." });

            // --- CORREÇÃO: Data de hoje convertida para UTC para comparação ---
            var hojeUtc = DateTime.Today.ToUniversalTime();
            // ---------------------------------------------------------------

            var registo = await _context.Horarios.FirstOrDefaultAsync(r => r.UtilizadorId == utilizadorId && r.Data.Date == hojeUtc.Date && r.HoraEntrada != TimeSpan.Zero && r.HoraSaida == TimeSpan.Zero);

            if (registo == null) return BadRequest(new { success = false, message = "Registo de entrada não encontrado ou saída já efetuada." });
            if (registo.SaidaAlmoco != TimeSpan.Zero && registo.EntradaAlmoco == TimeSpan.Zero) return BadRequest(new { success = false, message = "Registe a entrada do almoço primeiro." });

            registo.HoraSaida = DateTime.Now.TimeOfDay; // TimeSpan está ok
            await _context.SaveChangesAsync();
            _logger.LogInformation("RegistarSaida: Saída do dia para {UserId} às {HoraSaida}.", utilizadorId, registo.HoraSaida);
            return Ok(new { success = true, message = "Saída registada com sucesso!", hora = registo.HoraSaida.ToString(@"hh\:mm\:ss") });
        }


        //
        // Bloco: Action para a View de Histórico de Registos
        //

        /// <summary>
        /// Apresenta a página com o histórico de todos os registos de ponto do utilizador autenticado.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> MeuRegisto(DateTime? filtroData)
        {
            ViewData["Title"] = "O Meu Registo de Ponto";
            var userId = GetCurrentUserId();

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Acesso a MeuRegisto sem UserID.");
                return Unauthorized("Utilizador não autenticado.");
            }

            IQueryable<Horario> query = _context.Horarios.Where(h => h.UtilizadorId == userId);

            // --- CORREÇÃO: Data do filtro convertida para UTC para comparação ---
            if (filtroData.HasValue)
            {
                var filtroDataUtc = filtroData.Value.ToUniversalTime();
                query = query.Where(h => h.Data.Date == filtroDataUtc.Date); // Compara com a data em UTC
            }
            // -----------------------------------------------------------------

            var horariosDoUsuario = await query.OrderByDescending(h => h.Data).ThenBy(h => h.HoraEntrada).ToListAsync();

            var viewModels = horariosDoUsuario.Select(h =>
            {
                TimeSpan totalTrabalhado = TimeSpan.Zero;
                TimeSpan tempoAlmoco = TimeSpan.Zero;
                if (h.EntradaAlmoco > h.SaidaAlmoco) tempoAlmoco = h.EntradaAlmoco - h.SaidaAlmoco;
                if (h.HoraSaida > h.HoraEntrada) totalTrabalhado = (h.HoraSaida - h.HoraEntrada) - tempoAlmoco;
                if (totalTrabalhado < TimeSpan.Zero) totalTrabalhado = TimeSpan.Zero;

                return new HorarioColaboradorViewModel
                {
                    HorarioId = h.Id,
                    Data = h.Data, // Esta data já vem da BD como UTC, não precisa de converter
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