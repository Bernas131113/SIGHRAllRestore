// Controllers/FeedbackController.cs
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

namespace SIGHR.Controllers
{
    [Authorize]
    public class FeedbackController : Controller
    {
        private readonly SIGHRContext _context;
        private readonly UserManager<SIGHRUser> _userManager;

        public FeedbackController(SIGHRContext context, UserManager<SIGHRUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==========================================
        // ÁREA DO COLABORADOR
        // ==========================================

        [HttpGet]
        [Authorize(Policy = "CollaboratorAccessUI")]
        public IActionResult Registar()
        {
            ViewData["Title"] = "Submeter Feedback";
            var model = new FeedbackViewModel();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CollaboratorAccessUI")]
        public async Task<IActionResult> Registar(FeedbackViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (ModelState.IsValid)
            {
                var feedback = new Feedback
                {
                    UtilizadorId = userId,
                    TipoSubmissao = model.TipoSubmissao,
                    Titulo = model.Titulo,
                    Descricao = model.Descricao,
                    DataRegisto = DateTime.UtcNow,
                    Estado = "Pendente"
                };

                _context.Feedbacks.Add(feedback);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "O seu feedback foi enviado com sucesso. Obrigado!";
                return RedirectToAction(nameof(MinhasSubmissoes));
            }

            // Se o modelo falhar, volta a mostrar o formulário
            ViewData["Title"] = "Submeter Feedback";
            return View(model);
        }

        [HttpGet]
        [Authorize(Policy = "CollaboratorAccessUI")]
        public async Task<IActionResult> MinhasSubmissoes()
        {
            ViewData["Title"] = "Minhas Submissões";
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var submissoes = await _context.Feedbacks
                .Where(f => f.UtilizadorId == userId)
                .OrderByDescending(f => f.DataRegisto)
                .ToListAsync();

            return View(submissoes);
        }

        // ==========================================
        // ÁREA DO ADMIN
        // ==========================================

        [HttpGet]
        [Authorize(Policy = "AdminAccessUI")]
        public async Task<IActionResult> GestaoAdmin()
        {
            ViewData["Title"] = "Gestão de Feedback";

            var submissoes = await _context.Feedbacks
                .Include(f => f.User) // Inclui o nome do utilizador
                .OrderByDescending(f => f.DataRegisto)
                .ToListAsync();

            return View(submissoes);
        }
    }
}