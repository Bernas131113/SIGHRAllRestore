// Controllers/FaltasController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIGHR.Models;
using SIGHR.Models.ViewModels;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SIGHR.Areas.Identity.Data;

namespace SIGHR.Controllers
{
    [Authorize]
    public class FaltasController : Controller
    {
        private readonly SIGHRContext _context;
        private readonly UserManager<SIGHRUser> _userManager;
        private readonly ILogger<FaltasController> _logger;

        public FaltasController(SIGHRContext context, UserManager<SIGHRUser> userManager, ILogger<FaltasController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Policy = "CollaboratorAccessUI")]
        public IActionResult Registar()
        {
            var model = new FaltaViewModel
            {
                DataFalta = DateTime.Today,
                Inicio = DateTime.Today,
                Fim = DateTime.Today.AddHours(1),
                Motivo = string.Empty
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CollaboratorAccessUI")]
        public async Task<IActionResult> Registar(FaltaViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Utilizador não autenticado.");

            if (model.Inicio.TimeOfDay >= model.Fim.TimeOfDay)
            {
                ModelState.AddModelError(nameof(model.Fim), "A hora de fim deve ser posterior à hora de início.");
            }

            if (ModelState.IsValid)
            {
                var currentUser = await _userManager.FindByIdAsync(userId);
                string userNameForLog = currentUser?.UserName ?? "UtilizadorDesconhecido";

                var falta = new Falta
                {
                    UtilizadorId = userId,
                    Data = DateTime.UtcNow,
                    DataFalta = model.DataFalta.ToUniversalTime(),
                    Inicio = model.Inicio.ToUniversalTime(), // <<-- AGORA ESTÁ CORRETO: DateTime para DateTime
                    Fim = model.Fim.ToUniversalTime(),       // <<-- AGORA ESTÁ CORRETO: DateTime para DateTime
                    Motivo = model.Motivo
                };

                _context.Faltas.Add(falta);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Falta registada com sucesso pelo utilizador '{UserNameForLog}' (ID: {UserId}).", userNameForLog, userId);
                TempData["SuccessMessage"] = "Falta registada com sucesso!";
                return RedirectToAction(nameof(MinhasFaltas));
            }
            return View(model);
        }

        [HttpGet]
        [Authorize(Policy = "CollaboratorAccessUI")]
        public async Task<IActionResult> MinhasFaltas()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Utilizador não autenticado.");

            IQueryable<Falta> query = _context.Faltas
                .Where(f => f.UtilizadorId == userId)
                .Include(f => f.User);

            var faltasDoUsuario = await query
                .OrderByDescending(f => f.DataFalta).ThenBy(f => f.Inicio)
                .Select(f => new FaltaComUserNameViewModel
                {
                    Id = f.Id,
                    DataFalta = f.DataFalta,
                    Inicio = f.Inicio, // Agora é DateTime
                    Fim = f.Fim,       // Agora é DateTime
                    Motivo = f.Motivo,
                    DataRegisto = f.Data,
                    UserName = f.User != null ? (f.User.NomeCompleto ?? f.User.UserName ?? "N/D") : "N/D"
                })
                .ToListAsync();
            return View(faltasDoUsuario);
        }

        // --- ACTIONS PARA ADMINISTRAÇÃO ---
        [HttpGet]
        [Authorize(Policy = "AdminAccessUI")]
        public IActionResult GestaoAdmin()
        {
            ViewData["Title"] = "Gestão de Todas as Faltas";
            return View();
        }
    }
}