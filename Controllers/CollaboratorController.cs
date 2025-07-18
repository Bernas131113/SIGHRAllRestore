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
using System; // Adicionado para DateTime

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

            // ---- CORREÇÃO APLICADA AQUI ----
            // Converte DateTime.Today para UTC antes de usar na query para evitar erro de fuso horário com PostgreSQL.
            var hojeUtc = DateTime.Today.ToUniversalTime();
            // --------------------------------

            var user = await _context.Users
                // Compara a data do Horário (que é 'timestamp with time zone' no PostgreSQL) com a data de hoje em UTC.
                .Include(u => u.Horarios.Where(h => h.Data.Date == hojeUtc.Date))
                .Include(u => u.Faltas.OrderByDescending(f => f.DataFalta).Take(5))
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return NotFound("Utilizador não encontrado.");

            var viewModel = new CollaboratorDashboardViewModel
            {
                NomeCompleto = user.NomeCompleto ?? user.UserName,
                HorarioDeHoje = user.Horarios.FirstOrDefault(),
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