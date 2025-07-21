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
                Inicio = DateTime.Today.Date.AddHours(9),
                Fim = DateTime.Today.Date.AddHours(18),
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

                // ---- CORREÇÃO FINAL E SIMPLIFICADA ----
                var dataFormulario = model.DataFalta.Date;

                DateTime CombineAndConvertToUtc(DateTime date, DateTime timeFromForm)
                {
                    if (timeFromForm.TimeOfDay == TimeSpan.Zero) return DateTime.MinValue.ToUniversalTime();
                    var dateTimeUnspecified = new DateTime(date.Year, date.Month, date.Day, timeFromForm.Hour, timeFromForm.Minute, timeFromForm.Second);
                    return dateTimeUnspecified.ToUniversalTime();
                }

                var falta = new Falta
                {
                    UtilizadorId = userId,
                    Data = DateTime.UtcNow,
                    DataFalta = DateTime.SpecifyKind(dataFormulario, DateTimeKind.Utc),
                    Inicio = CombineAndConvertToUtc(dataFormulario, model.Inicio),
                    Fim = CombineAndConvertToUtc(dataFormulario, model.Fim),
                    Motivo = model.Motivo
                };
                // ------------------------------------

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
                    Inicio = f.Inicio,
                    Fim = f.Fim,
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