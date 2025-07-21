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
    [Authorize(Policy = "AdminAccessUI")]
    public class AdminController : Controller
    {
        private readonly SIGHRContext _context;
        private readonly UserManager<SIGHRUser> _userManager;
        private readonly ILogger<AdminController> _logger;

        public AdminController(SIGHRContext context, UserManager<SIGHRUser> userManager, ILogger<AdminController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string? filtroNome, DateTime? filtroData)
        {
            ViewData["Title"] = "Gestão de Registos de Ponto";
            IQueryable<Horario> query = _context.Horarios.Include(h => h.User);

            if (!string.IsNullOrEmpty(filtroNome))
            {
                query = query.Where(h => h.User != null && ((h.User.UserName != null && h.User.UserName.Contains(filtroNome)) || (h.User.NomeCompleto != null && h.User.NomeCompleto.Contains(filtroNome))));
            }

            if (filtroData.HasValue)
            {
                var filtroDataUtc = filtroData.Value.ToUniversalTime();
                query = query.Where(h => h.Data.Date == filtroDataUtc.Date);
            }

            var horarios = await query.OrderByDescending(h => h.Data).ThenBy(h => h.User != null ? h.User.UserName ?? "" : "").ThenBy(h => h.HoraEntrada).ToListAsync();

            var viewModels = horarios.Select(h => {
                TimeSpan totalTrabalhado = TimeSpan.Zero, tempoAlmoco = TimeSpan.Zero;
                TimeSpan horaEntradaTs = h.HoraEntrada.TimeOfDay;
                TimeSpan saidaAlmocoTs = h.SaidaAlmoco.TimeOfDay;
                TimeSpan entradaAlmocoTs = h.EntradaAlmoco.TimeOfDay;
                TimeSpan horaSaidaTs = h.HoraSaida.TimeOfDay;

                if (h.EntradaAlmoco > h.SaidaAlmoco && h.SaidaAlmoco != DateTime.MinValue.ToUniversalTime()) tempoAlmoco = entradaAlmocoTs - saidaAlmocoTs;
                if (h.HoraSaida > h.HoraEntrada && h.HoraEntrada != DateTime.MinValue.ToUniversalTime())
                {
                    totalTrabalhado = (horaSaidaTs - horaEntradaTs) - tempoAlmoco;
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

        [HttpGet]
        public async Task<IActionResult> DownloadHorariosExcel(string? filtroNome, DateTime? filtroData)
        {
            IQueryable<Horario> query = _context.Horarios.Include(h => h.User);
            if (!string.IsNullOrEmpty(filtroNome)) query = query.Where(h => h.User != null && ((h.User.UserName != null && h.User.UserName.Contains(filtroNome)) || (h.User.NomeCompleto != null && h.User.NomeCompleto.Contains(filtroNome))));
            if (filtroData.HasValue)
            {
                var filtroDataUtc = filtroData.Value.ToUniversalTime();
                query = query.Where(h => h.Data.Date == filtroDataUtc.Date);
            }

            var horariosParaExportar = await query.OrderByDescending(h => h.Data).ThenBy(h => h.User != null ? h.User.UserName ?? "" : "").ThenBy(h => h.HoraEntrada).Select(h => new { NomeUtilizador = h.User != null ? (h.User.NomeCompleto ?? h.User.UserName ?? "Desconhecido") : "Desconhecido", h.Data, h.HoraEntrada, h.SaidaAlmoco, h.EntradaAlmoco, h.HoraSaida }).ToListAsync();
            string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            string fileName = $"RegistosPonto_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            using (var workbook = new XLWorkbook())
            {
                IXLWorksheet worksheet = workbook.Worksheets.Add("Registos de Ponto");
                worksheet.Cell(1, 1).Value = "Utilizador"; worksheet.Cell(1, 2).Value = "Data"; worksheet.Cell(1, 3).Value = "Entrada"; worksheet.Cell(1, 4).Value = "Saída Almoço"; worksheet.Cell(1, 5).Value = "Entrada Almoço"; worksheet.Cell(1, 6).Value = "Saída"; worksheet.Cell(1, 7).Value = "Total Horas";
                var headerRow = worksheet.Row(1);
                headerRow.Style.Font.Bold = true; headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;
                int currentRow = 2;
                foreach (var item in horariosParaExportar)
                {
                    worksheet.Cell(currentRow, 1).SetValue(item.NomeUtilizador);
                    worksheet.Cell(currentRow, 2).SetValue(item.Data.ToLocalTime().ToString("dd/MM/yyyy")); // Data em formato PT

                    if (item.HoraEntrada.Year > 1)
                        worksheet.Cell(currentRow, 3).SetValue(item.HoraEntrada.ToLocalTime().ToString("HH:mm"));
                    if (item.SaidaAlmoco.Year > 1)
                        worksheet.Cell(currentRow, 4).SetValue(item.SaidaAlmoco.ToLocalTime().ToString("HH:mm"));
                    if (item.EntradaAlmoco.Year > 1)
                        worksheet.Cell(currentRow, 5).SetValue(item.EntradaAlmoco.ToLocalTime().ToString("HH:mm"));
                    if (item.HoraSaida.Year > 1)
                        worksheet.Cell(currentRow, 6).SetValue(item.HoraSaida.ToLocalTime().ToString("HH:mm"));

                    TimeSpan totalTrabalhadoExcel = TimeSpan.Zero;
                    if (item.HoraEntrada.Year > 1 && item.HoraSaida.Year > 1)
                    {
                        TimeSpan tempoAlmocoExcel = TimeSpan.Zero;
                        if (item.EntradaAlmoco > item.SaidaAlmoco) tempoAlmocoExcel = item.EntradaAlmoco.TimeOfDay - item.SaidaAlmoco.TimeOfDay;
                        totalTrabalhadoExcel = (item.HoraSaida.TimeOfDay - item.HoraEntrada.TimeOfDay) - tempoAlmocoExcel;
                        if (totalTrabalhadoExcel < TimeSpan.Zero) totalTrabalhadoExcel = TimeSpan.Zero;
                    }
                    if (totalTrabalhadoExcel > TimeSpan.Zero)
                        worksheet.Cell(currentRow, 7).SetValue(totalTrabalhadoExcel); // O Excel formata TimeSpan corretamente

                    currentRow++;
                }

                for (int i = 1; i <= 7; i++) worksheet.Column(i).AdjustToContents();
                using (var stream = new MemoryStream()) { workbook.SaveAs(stream); return File(stream.ToArray(), contentType, fileName); }
            }
        }


        //
        // Bloco: Edição de Registos de Ponto
        //

        [HttpGet]
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null) return NotFound();

            var horario = await _context.Horarios.Include(h => h.User).FirstOrDefaultAsync(h => h.Id == id);
            if (horario == null) return NotFound();

            // ---- CORREÇÃO AQUI: Passa os DateTimes em UTC diretamente para o ViewModel ----
            // O JavaScript na View irá tratar da conversão para a hora local.
            var viewModel = new EditHorarioViewModel
            {
                Id = horario.Id,
                NomeUtilizador = horario.User?.NomeCompleto ?? horario.User?.UserName,
                Data = horario.Data, // A data em UTC
                HoraEntrada = horario.HoraEntrada,
                SaidaAlmoco = horario.SaidaAlmoco,
                EntradaAlmoco = horario.EntradaAlmoco,
                HoraSaida = horario.HoraSaida
            };
            // --------------------------------------------------------------------------

            ViewData["Title"] = $"Editar Registo de {horario.Data.ToLocalTime():dd/MM/yyyy}";
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, EditHorarioViewModel viewModel)
        {
            if (id != viewModel.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var horarioNaBd = await _context.Horarios.FindAsync(id);
                    if (horarioNaBd == null) return NotFound();

                    // ---- CORREÇÃO FINAL (SIMPLIFICADA) ----
                    // 1. Obter a data do formulário. O Kind será 'Unspecified'.
                    var dataFormulario = viewModel.Data;

                    // 2. Função auxiliar para combinar a data com a hora de forma segura.
                    DateTime CombineAndConvertToUtc(DateTime date, DateTime timeFromForm)
                    {
                        if (timeFromForm.TimeOfDay == TimeSpan.Zero && timeFromForm.Year == 1)
                        {
                            return DateTime.MinValue.ToUniversalTime();
                        }

                        // CRUCIAL: Combina a data e a hora do formulário para criar um DateTime LOCAL.
                        var dateTimeLocal = new DateTime(date.Year, date.Month, date.Day,
                                                         timeFromForm.Hour, timeFromForm.Minute, timeFromForm.Second,
                                                         DateTimeKind.Local); // Força o Kind para Local (fuso do servidor)

                        // Converte este novo DateTime local para UTC.
                        return dateTimeLocal.ToUniversalTime();
                    }

                    // Atribui a data diretamente, sem conversão de fuso horário.
                    // O Entity Framework/PostgreSQL vai tratá-la corretamente como uma data UTC `00:00:00`.
                    horarioNaBd.Data = DateTime.SpecifyKind(dataFormulario.Date, DateTimeKind.Utc); // <<< CORREÇÃO PRINCIPAL AQUI

                    horarioNaBd.HoraEntrada = CombineAndConvertToUtc(dataFormulario, viewModel.HoraEntrada);
                    horarioNaBd.SaidaAlmoco = CombineAndConvertToUtc(dataFormulario, viewModel.SaidaAlmoco);
                    horarioNaBd.EntradaAlmoco = CombineAndConvertToUtc(dataFormulario, viewModel.EntradaAlmoco);
                    horarioNaBd.HoraSaida = CombineAndConvertToUtc(dataFormulario, viewModel.HoraSaida);
                    // ------------------------------------

                    _context.Update(horarioNaBd);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Horarios.Any(e => e.Id == viewModel.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["Title"] = $"Editar Registo de {viewModel.Data:dd/MM/yyyy}";
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("AdminLoginScheme");
            return RedirectToPage("/Account/AdminLogin", new { area = "Identity" });
        }
    }
}