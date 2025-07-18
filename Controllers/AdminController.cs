// Controllers/AdminController.cs
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIGHR.Models;
using SIGHR.Models.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ClosedXML.Excel;
using SIGHR.Areas.Identity.Data;

namespace SIGHR.Controllers
{
    /// <summary>
    /// Controlador para a área de administração da aplicação.
    /// Acesso restrito a utilizadores com a função "Admin", definido pela política "AdminAccessUI".
    /// </summary>
    [Authorize(Policy = "AdminAccessUI")]
    public class AdminController : Controller
    {
        //
        // Bloco: Injeção de Dependências
        //
        private readonly SIGHRContext _context;
        private readonly UserManager<SIGHRUser> _userManager;
        private readonly ILogger<AdminController> _logger;

        public AdminController(SIGHRContext context, UserManager<SIGHRUser> userManager, ILogger<AdminController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        //
        // Bloco: Gestão de Registos de Ponto (Visualização e Filtros)
        //

        /// <summary>
        /// Action principal que exibe a página de gestão de todos os registos de ponto.
        /// Permite filtrar os resultados por nome de utilizador e/ou data.
        /// </summary>
        /// <param name="filtroNome">Texto para pesquisar no nome de utilizador ou nome completo.</param>
        /// <param name="filtroData">Data específica para filtrar os registos.</param>
        /// <returns>A View 'Index' com a lista de horários filtrada.</returns>
        [HttpGet]
        public async Task<IActionResult> Index(string? filtroNome, DateTime? filtroData)
        {
            ViewData["Title"] = "Gestão de Registos de Ponto";

            IQueryable<Horario> query = _context.Horarios.Include(h => h.User);

            if (!string.IsNullOrEmpty(filtroNome))
            {
                query = query.Where(h => h.User != null && ((h.User.UserName != null && h.User.UserName.Contains(filtroNome)) || (h.User.NomeCompleto != null && h.User.NomeCompleto.Contains(filtroNome))));
            }

            // --- CORREÇÃO: Data do filtro convertida para UTC para comparação ---
            if (filtroData.HasValue)
            {
                var filtroDataUtc = filtroData.Value.ToUniversalTime();
                query = query.Where(h => h.Data.Date == filtroDataUtc.Date);
            }
            // -----------------------------------------------------------------

            var horarios = await query.OrderByDescending(h => h.Data).ThenBy(h => h.User != null ? h.User.UserName ?? "" : "").ThenBy(h => h.HoraEntrada).ToListAsync();
            var viewModels = horarios.Select(h => {
                TimeSpan totalTrabalhado = TimeSpan.Zero, tempoAlmoco = TimeSpan.Zero;
                if (h.EntradaAlmoco > h.SaidaAlmoco && h.SaidaAlmoco != TimeSpan.Zero) tempoAlmoco = h.EntradaAlmoco - h.SaidaAlmoco;
                if (h.HoraSaida > h.HoraEntrada && h.HoraEntrada != TimeSpan.Zero)
                {
                    totalTrabalhado = (h.HoraSaida - h.HoraEntrada) - tempoAlmoco;
                    if (totalTrabalhado < TimeSpan.Zero) totalTrabalhado = TimeSpan.Zero;
                }
                return new HorarioAdminViewModel
                {
                    HorarioId = h.Id,
                    NomeUtilizador = h.User != null ? (h.User.NomeCompleto ?? h.User.UserName ?? "Desconhecido") : "Desconhecido",
                    Data = h.Data,
                    HoraEntrada = h.HoraEntrada,
                    SaidaAlmoco = h.SaidaAlmoco,
                    EntradaAlmoco = h.EntradaAlmoco,
                    HoraSaida = h.HoraSaida,
                    TotalHorasTrabalhadas = totalTrabalhado > TimeSpan.Zero ? $"{(int)totalTrabalhado.TotalHours:D2}:{totalTrabalhado.Minutes:D2}" : "--:--"
                };
            }).ToList();
            ViewData["FiltroNomeAtual"] = filtroNome;
            ViewData["FiltroDataAtual"] = filtroData?.ToString("yyyy-MM-dd");
            return View(viewModels);
        }

        //
        // Bloco: Edição de Registos de Ponto
        //

        /// <summary>
        /// Action GET para exibir o formulário de edição de um registo de ponto específico.
        /// </summary>
        /// <param name="id">O ID do registo de ponto a ser editado.</param>
        /// <returns>A View 'Edit' com os dados do registo.</returns>
        [HttpGet]
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound("ID do registo de ponto não fornecido.");
            }

            var horario = await _context.Horarios
                                        .Include(h => h.User) // Incluir o utilizador para mostrar o nome
                                        .FirstOrDefaultAsync(h => h.Id == id);

            if (horario == null)
            {
                return NotFound($"Registo de ponto com ID {id} não encontrado.");
            }

            var viewModel = new EditHorarioViewModel
            {
                Id = horario.Id,
                NomeUtilizador = horario.User?.NomeCompleto ?? horario.User?.UserName ?? "Desconhecido",
                Data = horario.Data, // Data como vem da BD (UTC)
                HoraEntrada = horario.HoraEntrada,
                SaidaAlmoco = horario.SaidaAlmoco,
                EntradaAlmoco = horario.EntradaAlmoco,
                HoraSaida = horario.HoraSaida
            };

            // Ajusta o ViewData para o título da página de edição
            ViewData["Title"] = $"Editar Registo de Ponto: {viewModel.NomeUtilizador} ({viewModel.Data.ToLocalTime():dd/MM/yyyy})";
            return View(viewModel);
        }

        /// <summary>
        /// Action POST para processar a submissão do formulário de edição de um registo de ponto.
        /// </summary>
        /// <param name="model">O ViewModel com os dados atualizados do registo de ponto.</param>
        /// <returns>Redireciona para a lista de registos ou retorna a View com erros.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditHorarioViewModel model)
        {
            if (ModelState.IsValid)
            {
                var horarioToUpdate = await _context.Horarios.FindAsync(model.Id);

                if (horarioToUpdate == null)
                {
                    _logger.LogWarning("Tentativa de editar registo de ponto com ID {HorarioId} que não foi encontrado.", model.Id);
                    return NotFound("Registo de ponto não encontrado para atualização.");
                }

                // --- ATENÇÃO: Convertendo a Data do formulário para UTC antes de guardar ---
                // Data do formulário vem como Unspecified, converter para UTC para consistência
                horarioToUpdate.Data = model.Data.ToUniversalTime();
                // -----------------------------------------------------------------------

                // Atualiza as horas (TimeSpan não tem informação de fuso horário, está ok)
                horarioToUpdate.HoraEntrada = model.HoraEntrada;
                horarioToUpdate.SaidaAlmoco = model.SaidaAlmoco;
                horarioToUpdate.EntradaAlmoco = model.EntradaAlmoco;
                horarioToUpdate.HoraSaida = model.HoraSaida;

                try
                {
                    _context.Update(horarioToUpdate);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Registo de ponto atualizado com sucesso!";
                    _logger.LogInformation("Registo de ponto ID {HorarioId} atualizado com sucesso pelo utilizador Admin.", model.Id);
                    return RedirectToAction(nameof(Index)); // Redireciona de volta para a lista de registos
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Horarios.Any(e => e.Id == model.Id))
                    {
                        return NotFound("Registo de ponto não encontrado após tentativa de atualização.");
                    }
                    else
                    {
                        throw; // Relança o erro de concorrência se o registo existe mas houve um conflito
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao atualizar registo de ponto ID {HorarioId}.", model.Id);
                    ModelState.AddModelError("", "Ocorreu um erro ao atualizar o registo de ponto. Por favor, tente novamente.");
                }
            }
            // Se o modelo não for válido, retorna a view com os erros
            ViewData["Title"] = $"Editar Registo de Ponto: {model.NomeUtilizador} ({model.Data.ToLocalTime():dd/MM/yyyy})"; // Repopula o título
            return View(model);
        }

        //
        // Bloco: Geração de Ficheiro Excel
        //

        /// <summary>
        /// Action para gerar e transferir um ficheiro Excel com os registos de ponto.
        /// Utiliza os mesmos filtros da página 'Index'.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DownloadHorariosExcel(string? filtroNome, DateTime? filtroData)
        {
            // A lógica de filtragem é idêntica à da action Index.
            IQueryable<Horario> query = _context.Horarios.Include(h => h.User);
            if (!string.IsNullOrEmpty(filtroNome)) query = query.Where(h => h.User != null && ((h.User.UserName != null && h.User.UserName.Contains(filtroNome)) || (h.User.NomeCompleto != null && h.User.NomeCompleto.Contains(filtroNome))));

            // --- CORREÇÃO: Data do filtro convertida para UTC para comparação ---
            if (filtroData.HasValue)
            {
                var filtroDataUtc = filtroData.Value.ToUniversalTime();
                query = query.Where(h => h.Data.Date == filtroDataUtc.Date);
            }
            // -----------------------------------------------------------------

            var horariosParaExportar = await query
                .OrderByDescending(h => h.Data)
                .ThenBy(h => h.User != null ? h.User.UserName ?? "" : "")
                .ThenBy(h => h.HoraEntrada)
                .Select(h => new {
                    NomeUtilizador = h.User != null ? (h.User.NomeCompleto ?? h.User.UserName ?? "Desconhecido") : "Desconhecido",
                    h.Data,
                    h.HoraEntrada,
                    h.SaidaAlmoco,
                    h.EntradaAlmoco,
                    h.HoraSaida
                })
                .ToListAsync();

            string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            string fileName = $"RegistosPonto_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            // Utiliza a biblioteca ClosedXML para criar o ficheiro Excel em memória.
            using (var workbook = new XLWorkbook())
            {
                IXLWorksheet worksheet = workbook.Worksheets.Add("Registos de Ponto");

                // Cabeçalho da folha de cálculo
                worksheet.Cell(1, 1).Value = "Utilizador"; worksheet.Cell(1, 2).Value = "Data"; worksheet.Cell(1, 3).Value = "Entrada"; worksheet.Cell(1, 4).Value = "Saída Almoço"; worksheet.Cell(1, 5).Value = "Entrada Almoço"; worksheet.Cell(1, 6).Value = "Saída"; worksheet.Cell(1, 7).Value = "Total Horas";
                var headerRow = worksheet.Row(1);
                headerRow.Style.Font.Bold = true; headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

                // Preenche as linhas com os dados dos horários.
                int currentRow = 2;
                foreach (var item in horariosParaExportar)
                {
                    worksheet.Cell(currentRow, 1).SetValue(item.NomeUtilizador);
                    worksheet.Cell(currentRow, 2).SetValue(item.Data).Style.NumberFormat.Format = "dd/MM/yyyy";
                    if (item.HoraEntrada != TimeSpan.Zero) worksheet.Cell(currentRow, 3).SetValue(item.HoraEntrada).Style.NumberFormat.Format = "hh:mm";
                    if (item.SaidaAlmoco != TimeSpan.Zero) worksheet.Cell(currentRow, 4).SetValue(item.SaidaAlmoco).Style.NumberFormat.Format = "hh:mm";
                    if (item.EntradaAlmoco != TimeSpan.Zero) worksheet.Cell(currentRow, 5).SetValue(item.EntradaAlmoco).Style.NumberFormat.Format = "hh:mm";
                    if (item.HoraSaida != TimeSpan.Zero) worksheet.Cell(currentRow, 6).SetValue(item.HoraSaida).Style.NumberFormat.Format = "hh:mm";

                    // Recalcula o total de horas para o Excel.
                    TimeSpan totalTrabalhadoExcel = TimeSpan.Zero, tempoAlmocoExcel = TimeSpan.Zero;
                    if (item.EntradaAlmoco > item.SaidaAlmoco && item.SaidaAlmoco != TimeSpan.Zero) tempoAlmocoExcel = item.EntradaAlmoco - item.SaidaAlmoco;
                    if (item.HoraSaida > item.HoraEntrada && item.HoraEntrada != TimeSpan.Zero) { totalTrabalhadoExcel = (item.HoraSaida - item.HoraEntrada) - tempoAlmocoExcel; if (totalTrabalhadoExcel < TimeSpan.Zero) totalTrabalhadoExcel = TimeSpan.Zero; }
                    if (totalTrabalhadoExcel > TimeSpan.Zero) worksheet.Cell(currentRow, 7).SetValue(totalTrabalhadoExcel).Style.NumberFormat.Format = "[h]:mm";

                    currentRow++;
                }

                // Ajusta a largura das colunas ao conteúdo.
                for (int i = 1; i <= 7; i++) worksheet.Column(i).AdjustToContents();

                // Guarda o ficheiro numa stream de memória e retorna-o ao utilizador para transferência.
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(stream.ToArray(), contentType, fileName);
                }
            }
        }

        //
        // Bloco: Autenticação
        //

        /// <summary>
        /// Action para realizar o logout do administrador.
        /// Remove o cookie de autenticação do esquema "AdminLoginScheme".
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("AdminLoginScheme");
            return RedirectToPage("/Account/AdminLogin", new { area = "Identity" });
        }
    }
}