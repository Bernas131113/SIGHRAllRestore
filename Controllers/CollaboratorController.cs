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
        private readonly SignInManager<SIGHRUser> _signInManager;
        private readonly IPasswordHasher<SIGHRUser> _pinHasher;
        private readonly ILogger<CollaboratorController> _logger;

        // ======================= O CONSTRUTOR CORRIGIDO ESTÁ AQUI =======================
        public CollaboratorController(
            SIGHRContext context,
            UserManager<SIGHRUser> userManager,
            SignInManager<SIGHRUser> signInManager, // <<< PARÂMETRO ADICIONADO
            IPasswordHasher<SIGHRUser> pinHasher,   // <<< PARÂMETRO ADICIONADO
            ILogger<CollaboratorController> logger)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager; // Agora esta variável existe
            _pinHasher = pinHasher;       // Agora esta variável existe
            _logger = logger;
        }
        // ============================================================================

        public async Task<IActionResult> Dashboard()
        {
            ViewData["Title"] = "Painel do Colaborador";
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Utilizador não identificado.");
            var user = await _context.Users.Include(u => u.Faltas.OrderByDescending(f => f.DataFalta).Take(5)).FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound("Utilizador não encontrado.");
            var hojeUtc = DateTime.UtcNow.Date;
            var horarioDeHoje = await _context.Horarios.FirstOrDefaultAsync(h => h.UtilizadorId == userId && h.Data.Date == hojeUtc);
            var viewModel = new CollaboratorDashboardViewModel { NomeCompleto = user.NomeCompleto ?? user.UserName, HorarioDeHoje = horarioDeHoje, UltimasFaltas = user.Faltas.ToList() };
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Manage()
        {
            ViewData["Title"] = "Gerir a Minha Conta";
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("Utilizador não encontrado.");

            var model = new CollaboratorManageViewModel
            {
                UserName = user.UserName!,
                NomeCompleto = user.NomeCompleto,
                Email = user.Email!
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Manage(CollaboratorManageViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("Utilizador não encontrado.");

            if (!ModelState.IsValid)
            {
                // Se o modelo não for válido para o perfil, retorna à view com os erros.
                ViewData["Title"] = "Gerir a Minha Conta";
                return View(model);
            }

            user.UserName = model.UserName;
            user.NomeCompleto = model.NomeCompleto;
            user.Email = model.Email;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                TempData["SuccessMessage"] = "O seu perfil foi atualizado com sucesso.";
                return RedirectToAction("Manage");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            ViewData["Title"] = "Gerir a Minha Conta";
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePin(CollaboratorManageViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("Utilizador não encontrado.");

            if (string.IsNullOrEmpty(model.OldPin) || string.IsNullOrEmpty(model.NewPin) || string.IsNullOrEmpty(model.ConfirmNewPin))
            {
                TempData["ErrorMessage"] = "Todos os campos do PIN são obrigatórios.";
                return RedirectToAction("Manage");
            }
            if (model.NewPin != model.ConfirmNewPin)
            {
                TempData["ErrorMessage"] = "O novo PIN e a confirmação não correspondem.";
                return RedirectToAction("Manage");
            }
            if (!System.Text.RegularExpressions.Regex.IsMatch(model.NewPin, @"^\d{4}$"))
            {
                TempData["ErrorMessage"] = "O novo PIN deve conter exatamente 4 números.";
                return RedirectToAction("Manage");
            }

            var verificationResult = _pinHasher.VerifyHashedPassword(user, user.PinnedHash!, model.OldPin);
            if (verificationResult == PasswordVerificationResult.Failed)
            {
                TempData["ErrorMessage"] = "O PIN atual está incorreto.";
                return RedirectToAction("Manage");
            }

            user.PinnedHash = _pinHasher.HashPassword(user, model.NewPin);
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "O seu PIN foi alterado com sucesso.";
            }
            else
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                TempData["ErrorMessage"] = $"Ocorreu um erro ao alterar o seu PIN: {errors}";
            }

            return RedirectToAction("Manage");
        }

        [HttpGet]
        public async Task<IActionResult> MinhaConta()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("Utilizador não encontrado.");
            ViewData["TemPerfilFacial"] = user.FacialProfile != null && user.FacialProfile.Length > 0;
            return View();
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