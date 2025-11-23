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
using System.Collections.Generic;
using System.Security.Claims;

namespace SIGHR.Controllers.Api
{
    public class MarcarFeriasRequest { public string Start { get; set; } = string.Empty; public string End { get; set; } = string.Empty; }
    public class ApagarFeriasRequest { public long Id { get; set; } }

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

        // 1. OBTER EVENTOS (A LÓGICA SIMPLES QUE FUNCIONAVA)
        [HttpGet("eventos")]
        public async Task<IActionResult> GetEventos(DateTime start, DateTime end)
        {
            try
            {
                if (start.Kind == DateTimeKind.Unspecified) start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
                if (end.Kind == DateTimeKind.Unspecified) end = DateTime.SpecifyKind(end, DateTimeKind.Utc);

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var feriasList = await _context.Ferias
                    .Where(f => f.DataInicio < end && f.DataFim >= start)
                    .Include(f => f.User)
                    .ToListAsync();

                var eventos = feriasList.Select(f => new {
                    id = f.Id,
                    title = f.User != null ? f.User.NomeCompleto ?? f.User.UserName : "Desconhecido",
                    start = f.DataInicio.ToString("yyyy-MM-dd"),
                    end = f.DataFim.AddDays(1).ToString("yyyy-MM-dd"),
                    color = (f.Tipo == "Empresa") ? "#dc3545" : "#0d6efd",

                    // A LÓGICA ORIGINAL: É meu e não é empresa? Então é editável.
                    editable = (f.UtilizadorId == userId && f.Tipo != "Empresa")
                });

                return Ok(eventos);
            }
            catch { return StatusCode(500, new { message = "Erro ao carregar eventos." }); }
        }

        // 2. APAGAR FÉRIAS (A LÓGICA SIMPLES QUE FUNCIONAVA)
        [HttpPost("apagar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApagarFerias([FromBody] ApagarFeriasRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var ferias = await _context.Ferias.Include(f => f.User).FirstOrDefaultAsync(f => f.Id == request.Id);

            if (ferias == null) return NotFound(new { message = "Não encontrado." });

            // Validação simples
            if (ferias.UtilizadorId != userId || ferias.Tipo == "Empresa")
                return BadRequest(new { message = "Não tem permissão para apagar." });

            if (ferias.User != null) ferias.User.DiasFeriasDisponiveis += ferias.DiasGastos;

            _context.Ferias.Remove(ferias);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Apagado com sucesso.", diasRestantes = ferias.User?.DiasFeriasDisponiveis ?? 0 });
        }

        // 3. MARCAR FÉRIAS
        [HttpPost("marcar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarFerias([FromBody] MarcarFeriasRequest request)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User); // Aqui usamos UserManager para ter o objeto completo
                if (user == null) return Unauthorized();

                if (!DateTime.TryParse(request.Start, out var dI) || !DateTime.TryParse(request.End, out var dF)) return BadRequest(new { message = "Datas inválidas." });

                dI = DateTime.SpecifyKind(dI, DateTimeKind.Utc).Date;
                dF = DateTime.SpecifyKind(dF, DateTimeKind.Utc).Date;

                if (await _context.Ferias.AnyAsync(f => f.UtilizadorId == user.Id && f.DataInicio <= dF && f.DataFim >= dI))
                    return BadRequest(new { message = "Já existem férias neste período." });

                int dias = CalcularDiasUteis(dI, dF);
                if (dias <= 0) return BadRequest(new { message = "Selecione dias úteis." });
                if (user.DiasFeriasDisponiveis < dias) return BadRequest(new { message = "Saldo insuficiente." });

                user.DiasFeriasDisponiveis -= dias;
                _context.Ferias.Add(new Ferias { UtilizadorId = user.Id, DataInicio = dI, DataFim = dF, DiasGastos = dias, Tipo = "Individual" });
                await _context.SaveChangesAsync();
                return Ok(new { message = "Marcado com sucesso!", diasRestantes = user.DiasFeriasDisponiveis });
            }
            catch { return StatusCode(500, new { message = "Erro interno." }); }
        }

        // 4. SALDO E ADMIN (SEM ALTERAÇÕES)
        [HttpGet("diasrestantes")]
        public async Task<IActionResult> GetDiasRestantes()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            return Ok(new { dias = user.DiasFeriasDisponiveis });
        }

        [HttpPost("admin/marcarempresa")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminAccessUI")]
        public async Task<IActionResult> MarcarFeriasEmpresa([FromBody] MarcarFeriasRequest request)
        {
            if (!DateTime.TryParse(request.Start, out var dI) || !DateTime.TryParse(request.End, out var dF)) return BadRequest(new { message = "Datas inválidas." });
            dI = DateTime.SpecifyKind(dI, DateTimeKind.Utc).Date; dF = DateTime.SpecifyKind(dF, DateTimeKind.Utc).Date;
            int dias = CalcularDiasUteis(dI, dF);
            if (dias <= 0) return BadRequest(new { message = "Zero dias." });

            await using var tr = await _context.Database.BeginTransactionAsync();
            try
            {
                var users = await _userManager.Users.Where(u => u.IsActiveEmployee).Include(u => u.Ferias).ToListAsync();
                foreach (var u in users)
                {
                    var ovs = u.Ferias.Where(f => f.DataInicio <= dF && f.DataFim >= dI && f.Tipo != "Empresa").ToList();
                    int r = 0;
                    if (ovs.Any())
                    {
                        for (DateTime d = dI; d <= dF; d = d.AddDays(1)) if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday && ovs.Any(f => d >= f.DataInicio && d <= f.DataFim)) r++;
                        var rem = ovs.Where(f => f.DataInicio >= dI && f.DataFim <= dF).ToList();
                        if (rem.Any()) _context.Ferias.RemoveRange(rem);
                    }
                    u.DiasFeriasDisponiveis = u.DiasFeriasDisponiveis + r - dias;
                    _context.Ferias.Add(new Ferias { UtilizadorId = u.Id, DataInicio = dI, DataFim = dF, DiasGastos = dias, Tipo = "Empresa" });
                }
                await _context.SaveChangesAsync(); await tr.CommitAsync();
                return Ok(new { message = "Marcado para empresa." });
            }
            catch { await tr.RollbackAsync(); return StatusCode(500, new { message = "Erro." }); }
        }

        private int CalcularDiasUteis(DateTime s, DateTime e)
        {
            int d = 0;
            for (DateTime dt = s; dt <= e; dt = dt.AddDays(1))
                if (dt.DayOfWeek != DayOfWeek.Saturday && dt.DayOfWeek != DayOfWeek.Sunday) d++;
            return d;
        }
    }
}