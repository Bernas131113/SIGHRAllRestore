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
using SIGHR.Services;

namespace SIGHR.Controllers
{
    [Authorize]
    public class EncomendasController : Controller
    {
        private readonly SIGHRContext _context;
        private readonly UserManager<SIGHRUser> _userManager;
        private readonly ILogger<EncomendasController> _logger;
        private readonly IEmailService _emailService;


        private static readonly List<SelectListItem> ListaDeMateriaisFixa = new List<SelectListItem>
        {
            new SelectListItem { Value = "Tijolo 7", Text = "Tijolo 7" },
            new SelectListItem { Value = "Tijolo 11", Text = "Tijolo 11" },
            new SelectListItem { Value = "Tijolo 15", Text = "Tijolo 15" },
            new SelectListItem { Value = "Tijolo 22", Text = "Tijolo 22" },
            new SelectListItem { Value = "Areia do Rio", Text = "Areia do Rio" },
            new SelectListItem { Value = "Areia Amarela", Text = "Areia Amarela" },
            new SelectListItem { Value = "Blocos 10", Text = "Blocos 10" },
            new SelectListItem { Value = "Blocos 15", Text = "Blocos 15" },
            new SelectListItem { Value = "Blocos 20", Text = "Blocos 20" },
            new SelectListItem { Value = "Cimento", Text = "Cimento" },
            new SelectListItem { Value = "Cal hidráulica", Text = "Cal hidráulica" }
        };

        public EncomendasController(SIGHRContext context,
                                    UserManager<SIGHRUser> userManager,
                                    ILogger<EncomendasController> logger,
                                    IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _emailService = emailService;
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

            if (filtroData.HasValue)
            {
                var filtroDataUtc = DateTime.SpecifyKind(filtroData.Value.Date, DateTimeKind.Utc);
                query = query.Where(e => e.Data.Date == filtroDataUtc);
            }

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

                    // === ISTO É O IMPORTANTE: Sum(r => r.Quantidade) sem o (int) ===
                    QuantidadeTotalItens = (e.Requisicoes != null) ? e.Requisicoes.Sum(r => r.Quantidade) : 0,
                    // ================================================================

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
            var materiaisDisponiveis = await Task.FromResult(ListaDeMateriaisFixa);
            var viewModel = new RegistarEncomendaViewModel
            {
                DataEncomenda = DateTime.Today,
                ItensRequisicao = new List<ItemRequisicaoViewModel> { new ItemRequisicaoViewModel { Quantidade = 1 } },
                MateriaisDisponiveis = new SelectList(materiaisDisponiveis, "Value", "Text")
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
                var dataFormulario = model.DataEncomenda.Date;
                var novaEncomenda = new Encomenda
                {
                    UtilizadorId = userId,
                    Data = DateTime.SpecifyKind(dataFormulario, DateTimeKind.Utc),
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

                    // ================== ALTERAÇÃO 3: Enviar o e-mail para a nova lista ==================
                    try
                    {
                        var encomendaParaEmail = await _context.Encomendas
                            .Include(e => e.User)
                            .Include(e => e.Requisicoes!)
                                .ThenInclude(r => r.Material)
                            .AsNoTracking()
                            .FirstOrDefaultAsync(e => e.Id == novaEncomenda.Id);

                        if (encomendaParaEmail != null)
                        {
                            // 1. Cria a lista de destinatários
                            var recipientList = new List<string>
                            {
                                  "ialves@allrestore.pt",
                                  "bbexiga@allrestore.pt",
                                   "spequeno@allrestore.pt",
                                    "pleitao@allrestore.pt",
                                    "bernardomiguelalves34@gmail.com"

                            };

                            // 2. Envia a notificação para a lista
                            await _emailService.SendEncomendaNotificationAsync(encomendaParaEmail, recipientList);
                            _logger.LogInformation("Notificação de encomenda enviada para {Count} destinatários.", recipientList.Count);
                        }
                    }
                    catch (Exception exEmail)
                    {
                        _logger.LogError(exEmail, "Erro ao tentar enviar o e-mail da encomenda.");
                    }
                    // ================== FIM DA ALTERAÇÃO 3 ==================

                    TempData["SuccessMessage"] = "Encomenda registada com sucesso!";
                    return RedirectToAction("MinhasEncomendas");
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