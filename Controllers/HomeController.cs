using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SIGHR.Models;

namespace SIGHR.Controllers
{
    /// <summary>
    /// Controlador principal para as p�ginas p�blicas e gen�ricas da aplica��o,
    /// como a p�gina inicial (Homepage) e a p�gina de erro.
    /// </summary>
    public class HomeController : Controller
    {
        // O servi�o de logging, injetado no construtor.
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Action para a p�gina principal (Homepage) da aplica��o.
        /// </summary>
        /// <returns>A View 'Index'.</returns>
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// Action para a p�gina de Pol�tica de Privacidade.
        /// </summary>
        /// <returns>A View 'Privacy'.</returns>
        public IActionResult Privacy()
        {
            return View();
        }

        /// <summary>
        /// Action para a p�gina de erro.
        /// Apresenta um identificador �nico do pedido para fins de depura��o (debugging).
        /// </summary>
        // O atributo [ResponseCache] impede que esta p�gina seja guardada na cache do browser,
        // garantindo que o utilizador veja sempre a informa��o de erro mais recente.
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}