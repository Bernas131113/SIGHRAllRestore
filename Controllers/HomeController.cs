// Controllers/HomeController.cs
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SIGHR.Models;
using SIGHR.Areas.Identity.Data; // <-- ADICIONA ESTE
using Microsoft.EntityFrameworkCore; // <-- ADICIONA ESTE

namespace SIGHR.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly SIGHRContext _context; // <-- ADICIONA ESTE

        // ================== ALTERAÇÃO 1: Atualiza o Construtor ==================
        public HomeController(ILogger<HomeController> logger, SIGHRContext context) // <-- ADICIONA O context
        {
            _logger = logger;
            _context = context; // <-- ADICIONA ESTE
        }
        // ================== FIM DA ALTERAÇÃO 1 ==================


        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        // ================== ALTERAÇÃO 2: Adiciona este novo método ==================
        [HttpGet]
        [Route("/healthcheck")] // Define um URL fácil de usar: https://.../healthcheck
        public async Task<IActionResult> HealthCheck()
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
        // ================== FIM DA ALTERAÇÃO 2 ==================

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}