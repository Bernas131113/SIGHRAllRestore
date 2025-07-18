// Controllers/EncomendasController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SIGHR.Models;
using SIGHR.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SIGHR.Areas.Identity.Data;

namespace SIGHR.Controllers
{
    [Authorize]
    public class EncomendasController : Controller
    {
        private readonly SIGHRContext _context;
        private readonly UserManager<SIGHRUser> _userManager;
        private readonly ILogger<EncomendasController> _logger;

        private static readonly List<SelectListItem> ListaDeMateriaisFixa = new List<SelectListItem> { /* ... sua lista fixa ... */ };

        public EncomendasController(SIGHRContext context, UserManager<SIGHRUser> userManager, ILogger<EncomendasController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Policy = "AdminAccessUI")]
        public IActionResult Index()
        {
            ViewData["Title"] = "Gestão de Encomendas";
            return View();
        }

        [HttpGet]
        [Authorize(Policy = "CollaboratorAccessUI")]
        public async Task<IActionResult> MinhasEncomendas(DateTime? filtroData, string? filtroEstado)
        {
            ViewData["Title"] = "As Minhas Encomendas";
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Utilizador não autenticado.");

            IQueryable<Encomenda> query = _context.Encomendas
                .Include(e => e.Requisicoes)
                    .ThenInclude(r => r.Material)
                .Where(e => e.UtilizadorId == userId);

            // ---- CORREÇÃO APLICADA AQUI ----
            if (filtroData.HasValue)
            {
                var filtroDataUtc = filtroData.Value.ToUniversalTime(); // Converte o filtro para UTC
                query = query.Where(e => e.Data.Date == filtroDataUtc.Date); // Compara com a data em UTC
            }
            // --------------------------------

            if (!string.IsNullOrEmpty(filtroEstado)) query = query.Where(e => e.Estado == filtroEstado);

            var minhasEncomendasViewModels = await query
                .OrderByDescending(e => e.Data)
                .Select(e => new MinhaEncomendaViewModel
                {
                    EncomendaId = e.Id,
                    DataEncomenda = e.Data,
                    DescricaoResumida = (e.Requisicoes != null && e.Requisicoes.Any()) ?
                        string.Join(", ", e.Requisicoes
                            .Where(r => r.Material != null)
                            .Select(r => r.Material!.Descricao ?? "Item")
                            .Take(2)) + (e.Requisicoes.Count > 2 ? "..." : "") :
                        "Nenhum material",
                    QuantidadeTotalItens = (e.Requisicoes != null) ? e.Requisicoes.Sum(r => (int)r.Quantidade) : 0,
                    Estado = e.Estado ?? "Indefinido"
                })
                .ToListAsync();

            ViewData["FiltroDataAtual"] = filtroData?.ToString("yyyy-MM-dd");
            ViewData["FiltroEstadoAtual"] = filtroEstado;
            ViewBag.EstadosEncomenda = new List<string> { "Pendente", "Em Processamento", "Pronta para Envio", "Enviada", "Entregue", "Cancelada" };
            return View(minhasEncomendasViewModels);
        }

        [HttpGet]
        [Authorize(Policy = "CollaboratorAccessUI")]
        public async Task<IActionResult> Registar()
        {
            ViewData["Title"] = "Registar Nova Encomenda";
            var materiaisDisponiveis = await _context.Materiais // <<-- ADICIONAR ESTAS LINHAS
                                        .OrderBy(m => m.Descricao)
                                        .Select(m => new SelectListItem { Value = m.Descricao, Text = m.Descricao })
                                        .ToListAsync();
            var viewModel = new RegistarEncomendaViewModel
            {
                DataEncomenda = DateTime.Today.ToUniversalTime(),
                ItensRequisicao = new List<ItemRequisicaoViewModel> { new ItemRequisicaoViewModel { Quantidade = 1 } },
                MateriaisDisponiveis = new SelectList(materiaisDisponiveis, "Value", "Text") // <<-- ESTÁ CORRETO
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CollaboratorAccessUI")]
        public async Task<IActionResult> Registar(RegistarEncomendaViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Utilizador não autenticado.");

            model.MateriaisDisponiveis = new SelectList(ListaDeMateriaisFixa, "Value", "Text");

            bool itensSaoValidos = true;
            if (model.ItensRequisicao == null || !model.ItensRequisicao.Any())
            {
                ModelState.AddModelError("ItensRequisicao", "Adicione pelo menos um material à encomenda.");
                itensSaoValidos = false;
            }
            else
            {
                foreach (var item in model.ItensRequisicao)
                {
                    if (string.IsNullOrEmpty(item.NomeMaterialOuId)) { ModelState.AddModelError("", "Selecione um material para todos os itens."); itensSaoValidos = false; break; }
                    if (item.Quantidade <= 0) { ModelState.AddModelError("", "A quantidade de cada item deve ser maior que zero."); itensSaoValidos = false; break; }
                }
            }

            if (ModelState.IsValid && itensSaoValidos)
            {
                var novaEncomenda = new Encomenda
                {
                    UtilizadorId = userId,
                    Data = model.DataEncomenda.ToUniversalTime(), // <<-- CORREÇÃO APLICADA AQUI (data em UTC)
                    Estado = "Pendente",
                    Quantidade = model.ItensRequisicao!.Count,
                    DescricaoObra = model.DescricaoObra,
                    Requisicoes = new List<Requisicao>()
                };

                foreach (var itemVM in model.ItensRequisicao!)
                {
                    Material? materialEntity = await _context.Materiais.FirstOrDefaultAsync(m => m.Descricao == itemVM.NomeMaterialOuId);
                    if (materialEntity == null)
                    {
                        _logger.LogWarning("Material '{MaterialNome}' não encontrado. A criar novo material.", itemVM.NomeMaterialOuId);
                        materialEntity = new Material { Descricao = itemVM.NomeMaterialOuId! };
                        _context.Materiais.Add(materialEntity);
                    }
                    novaEncomenda.Requisicoes.Add(new Requisicao { Material = materialEntity, Quantidade = itemVM.Quantidade });
                }

                _context.Encomendas.Add(novaEncomenda);
                try
                {
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Encomenda registada com sucesso!";
                    return RedirectToAction(nameof(MinhasEncomendas));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao guardar a encomenda para o UserID: {UserId}.", userId);
                    ModelState.AddModelError("", "Ocorreu um erro ao guardar a encomenda. Por favor, tente novamente.");
                }
            }
            return View(model);
        }
    }
}