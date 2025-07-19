// Controllers/CollaboratorController.cs
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIGHR.Models.ViewModels;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SIGHR.Areas.Identity.Data;
using System;

namespace SIGHR.Controllers
{
    [Authorize(Policy = "CollaboratorAccessUI")]
    public class CollaboratorController : Controller
    {
        private readonly SIGHRContext _context;
        private readonly UserManager<SIGHRUser> _userManager;
        private readonly ILogger<CollaboratorController> _logger;

        public CollaboratorController(SIGHRContext context, UserManager<SIGHRUser> userManager, ILogger<CollaboratorController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> Dashboard()
        {
            ViewData["Title"] = "Painel do Colaborador";
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Utilizador não identificado.");

            // ---- LÓGICA DE CONSULTA SIMPLIFICADA E CORRIGIDA ----
            // 1. Obter o utilizador e as suas faltas.
            var user = await _context.Users
                .Include(u => u.Faltas.OrderByDescending(f => f.DataFalta).Take(5))
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return NotFound("Utilizador não encontrado.");

            // 2. Obter o registo de ponto de hoje SEPARADAMENTE e de forma explícita.
            var hojeUtc = DateTime.UtcNow.Date;
            var horarioDeHoje = await _context.Horarios
                .FirstOrDefaultAsync(h => h.UtilizadorId == userId && h.Data.Date == hojeUtc);
            // ----------------------------------------------------

            var viewModel = new CollaboratorDashboardViewModel
            {
                NomeCompleto = user.NomeCompleto ?? user.UserName,
                HorarioDeHoje = horarioDeHoje, // Atribui o horário encontrado (ou null se não houver)
                UltimasFaltas = user.Faltas.ToList()
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("CollaboratorLoginScheme");
            return RedirectToPage("/Account/CollaboratorPinLogin", new { area = "Identity" });
        }
    }
}