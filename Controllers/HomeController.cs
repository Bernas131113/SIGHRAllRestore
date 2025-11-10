// Controllers/HomeController.cs
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SIGHR.Models;
using SIGHR.Areas.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace SIGHR.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly SIGHRContext _context;

        public HomeController(ILogger<HomeController> logger, SIGHRContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        // ================== ALTERAÇÃO CRÍTICA AQUI ==================
        [AcceptVerbs("GET", "HEAD")] // <-- MUDADO DE [HttpGet]
        [Route("/healthcheck")]
        public async Task<IActionResult> HealthCheck()
        // ================== FIM DA ALTERAÇÃO ==================
        {
            try
            {
                // Esta é a consulta mais leve possível.
                // Apenas "toca" na base de dados para a acordar.
                await _context.Database.ExecuteSqlRawAsync("SELECT 1");

                // Se chegou aqui, o Render e o Supabase estão acordados.
                return Content("OK");
            }
            catch (Exception ex)
            {
                // Se falhar, retorna um erro 500 para o UptimeRobot saber.
                _logger.LogError(ex, "Health Check falhou!");
                return StatusCode(500, "Health Check falhou");
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}