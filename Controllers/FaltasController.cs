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
    /// <summary>
    /// Controlador responsável por servir as páginas (Views) relacionadas com faltas.
    /// A lógica de API foi movida para o FaltasApiController.
    /// </summary>
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

        // --- ACTIONS PARA COLABORADORES ---

        /// <summary>
        /// Apresenta o formulário para um utilizador registar uma falta para si próprio.
        /// </summary>
        [HttpGet]
        [Authorize(Policy = "CollaboratorAccessUI")]
        public IActionResult Registar()
        {
            var model = new FaltaViewModel
            {
                DataFalta = DateTime.Today // DataFalta aqui é local, será convertida no POST
            };
            return View(model);
        }

        /// <summary>
        /// Processa a submissão do formulário de registo de uma nova falta.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CollaboratorAccessUI")]
        public async Task<IActionResult> Registar(FaltaViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Utilizador não autenticado.");

            if (model.Inicio >= model.Fim)
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
                    Data = DateTime.Now.ToUniversalTime(), // --- CORREÇÃO: Data do registo guardada em UTC ---
                    DataFalta = model.DataFalta.ToUniversalTime(), // --- CORREÇÃO: Data da falta (do formulário) guardada em UTC ---
                    Inicio = model.Inicio,
                    Fim = model.Fim,
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

        /// <summary>
        /// Apresenta a lista de faltas do utilizador atualmente autenticado.
        /// </summary>
        [HttpGet]
        [Authorize(Policy = "CollaboratorAccessUI")]
        public async Task<IActionResult> MinhasFaltas()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Utilizador não autenticado.");

            IQueryable<Falta> query = _context.Faltas
                .Where(f => f.UtilizadorId == userId)
                .Include(f => f.User);

            // Não há filtro de data direto nesta action que precise de conversão UTC.
            // As datas DataFalta e DataRegisto no ViewModel já virão da BD como UTC e serão apresentadas como tal.

            var faltasDoUsuario = await query
                .OrderByDescending(f => f.DataFalta).ThenBy(f => f.Inicio)
                .Select(f => new FaltaComUserNameViewModel
                {
                    Id = f.Id,
                    DataFalta = f.DataFalta, // Esta data já vem da BD como UTC
                    Inicio = f.Inicio,
                    Fim = f.Fim,
                    Motivo = f.Motivo,
                    DataRegisto = f.Data, // Esta data já vem da BD como UTC
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