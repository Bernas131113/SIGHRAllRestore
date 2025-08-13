using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SIGHR.Controllers
{
    public class FeriasController : Controller
    {
        // Esta action serve a página de Colaborador
        [Authorize(Policy = "CollaboratorAccessUI")]
        public IActionResult Index()
        {
            ViewData["Title"] = "Calendário de Férias";
            return View();
        }

        // Esta action serve a página de Administrador
        [Authorize(Policy = "AdminAccessUI")]
        public IActionResult Gestao()
        {
            ViewData["Title"] = "Gestão de Férias";
            return View();
        }
    }
}