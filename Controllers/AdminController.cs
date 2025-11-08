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
using System.Globalization;

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
                    TotalHorasTrabalhadas = totalTrabalhado > TimeSpan.Zero ? $"{(int)totalTrabalhado.TotalHours:D2}:{totalTrabalhado.Minutes:D2}" : "--:--",

                    LatitudeEntrada = h.LatitudeEntrada,
                    LongitudeEntrada = h.LongitudeEntrada,
                    LatitudeSaidaAlmoco = h.LatitudeSaidaAlmoco,
                    LongitudeSaidaAlmoco = h.LongitudeSaidaAlmoco,
                    LatitudeEntradaAlmoco = h.LatitudeEntradaAlmoco,
                    LongitudeEntradaAlmoco = h.LongitudeEntradaAlmoco,
                    LatitudeSaida = h.LatitudeSaida,
                    LongitudeSaida = h.LongitudeSaida
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

            var horariosParaExportar = await query
                .OrderByDescending(h => h.Data)
                .ThenBy(h => h.User != null ? h.User.UserName ?? "" : "")
                .ThenBy(h => h.HoraEntrada)
                .Select(h => new
                {
                    NomeUtilizador = h.User != null ? (h.User.NomeCompleto ?? h.User.UserName ?? "Desconhecido") : "Desconhecido",
                    h.Data,
                    h.HoraEntrada,
                    h.SaidaAlmoco,
                    h.EntradaAlmoco,
                    h.HoraSaida,

                    h.LatitudeEntrada,
                    h.LongitudeEntrada,
                    h.LatitudeSaidaAlmoco,
                    h.LongitudeSaidaAlmoco,
                    h.LatitudeEntradaAlmoco,
                    h.LongitudeEntradaAlmoco,
                    h.LatitudeSaida,
                    h.LongitudeSaida
                }).ToListAsync();

            string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            string fileName = $"RegistosPonto_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            using (var workbook = new XLWorkbook())
            {
                IXLWorksheet worksheet = workbook.Worksheets.Add("Registos de Ponto");

                // --- Cabeçalho ---
                worksheet.Cell(1, 1).Value = "Utilizador";
                worksheet.Cell(1, 2).Value = "Data";
                worksheet.Cell(1, 3).Value = "Entrada";
                worksheet.Cell(1, 4).Value = "Saída Almoço";
                worksheet.Cell(1, 5).Value = "Entrada Almoço";
                worksheet.Cell(1, 6).Value = "Saída";
                worksheet.Cell(1, 7).Value = "Total Horas";
                worksheet.Cell(1, 8).Value = "Lat Entrada";
                worksheet.Cell(1, 9).Value = "Lon Entrada";
                worksheet.Cell(1, 10).Value = "Lat S. Almoço";
                worksheet.Cell(1, 11).Value = "Lon S. Almoço";
                worksheet.Cell(1, 12).Value = "Lat E. Almoço";
                worksheet.Cell(1, 13).Value = "Lon E. Almoço";
                worksheet.Cell(1, 14).Value = "Lat Saída";
                worksheet.Cell(1, 15).Value = "Lon Saída";
                worksheet.Cell(1, 16).Value = "Link Mapa Entrada";

                var headerRow = worksheet.Row(1);
                headerRow.Style.Font.Bold = true;
                headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

                // --- Dados ---
                int currentRow = 2;
                foreach (var item in horariosParaExportar)
                {
                    worksheet.Cell(currentRow, 1).SetValue(item.NomeUtilizador);
                    worksheet.Cell(currentRow, 2).SetValue(item.Data.ToLocalTime());
                    worksheet.Cell(currentRow, 3).SetValue(item.HoraEntrada.ToLocalTime());
                    worksheet.Cell(currentRow, 4).SetValue(item.SaidaAlmoco.ToLocalTime());
                    worksheet.Cell(currentRow, 5).SetValue(item.EntradaAlmoco.ToLocalTime());
                    worksheet.Cell(currentRow, 6).SetValue(item.HoraSaida.ToLocalTime());

                    TimeSpan totalTrabalhado = TimeSpan.Zero;
                    if (item.HoraEntrada.Year > 1 && item.HoraSaida.Year > 1)
                    {
                        TimeSpan tempoAlmoco = TimeSpan.Zero;
                        if (item.EntradaAlmoco > item.SaidaAlmoco)
                            tempoAlmoco = item.EntradaAlmoco.TimeOfDay - item.SaidaAlmoco.TimeOfDay;
                        totalTrabalhado = (item.HoraSaida.TimeOfDay - item.HoraEntrada.TimeOfDay) - tempoAlmoco;
                        if (totalTrabalhado < TimeSpan.Zero) totalTrabalhado = TimeSpan.Zero;
                    }
                    worksheet.Cell(currentRow, 7).SetValue(totalTrabalhado);


                    // ================== CORREÇÃO 1: ERROS CS1503 ==================
                    // A solução correta é simplesmente passar o double? (nullable)
                    // A biblioteca ClosedXML sabe como lidar com ele.

                    worksheet.Cell(currentRow, 8).SetValue(item.LatitudeEntrada);
                    worksheet.Cell(currentRow, 9).SetValue(item.LongitudeEntrada);
                    worksheet.Cell(currentRow, 10).SetValue(item.LatitudeSaidaAlmoco);
                    worksheet.Cell(currentRow, 11).SetValue(item.LongitudeSaidaAlmoco);
                    worksheet.Cell(currentRow, 12).SetValue(item.LatitudeEntradaAlmoco);
                    worksheet.Cell(currentRow, 13).SetValue(item.LongitudeEntradaAlmoco);
                    worksheet.Cell(currentRow, 14).SetValue(item.LatitudeSaida);
                    worksheet.Cell(currentRow, 15).SetValue(item.LongitudeSaida);

                    // ================== FIM DA CORREÇÃO 1 ====================


                    // ================== CORREÇÃO 2: AVISO CS8629 ==================
                    if (item.LatitudeEntrada.HasValue && item.LatitudeEntrada != 0)
                    {
                        // Adicionamos '!' para informar o compilador que, dentro deste 'if',
                        // sabemos que .Value não será nulo.
                        var lat = item.LatitudeEntrada!.Value.ToString("G", CultureInfo.InvariantCulture);
                        var lon = item.LongitudeEntrada!.Value.ToString("G", CultureInfo.InvariantCulture);
                        worksheet.Cell(currentRow, 16).FormulaA1 = $"HYPERLINK(\"https://www.google.com/maps?q={lat},{lon}\", \"Ver Mapa\")";
                        worksheet.Cell(currentRow, 16).Style.Font.FontColor = XLColor.Blue;
                        worksheet.Cell(currentRow, 16).Style.Font.Underline = XLFontUnderlineValues.Single;
                    }
                    // ================== FIM DA CORREÇÃO 2 ====================

                    currentRow++;
                }

                worksheet.Column(2).Style.NumberFormat.Format = "dd/mm/yyyy";
                worksheet.Column(3).Style.NumberFormat.Format = "HH:mm";
                worksheet.Column(4).Style.NumberFormat.Format = "HH:mm";
                worksheet.Column(5).Style.NumberFormat.Format = "HH:mm";
                worksheet.Column(6).Style.NumberFormat.Format = "HH:mm"; // Corrigido de Cell(1,6) para Column(6)
                worksheet.Column(7).Style.NumberFormat.Format = "[h]:mm";

                string formatCoordenadas = "0.000000";
                worksheet.Column(8).Style.NumberFormat.Format = formatCoordenadas;
                worksheet.Column(9).Style.NumberFormat.Format = formatCoordenadas;
                worksheet.Column(10).Style.NumberFormat.Format = formatCoordenadas;
                worksheet.Column(11).Style.NumberFormat.Format = formatCoordenadas;
                worksheet.Column(12).Style.NumberFormat.Format = formatCoordenadas;
                worksheet.Column(13).Style.NumberFormat.Format = formatCoordenadas;
                worksheet.Column(14).Style.NumberFormat.Format = formatCoordenadas;
                worksheet.Column(15).Style.NumberFormat.Format = formatCoordenadas;

                for (int i = 1; i <= 16; i++)
                {
                    worksheet.Column(i).AdjustToContents();
                }


                if (currentRow > 2)
                {
                    var totalRow = worksheet.Row(currentRow);
                    totalRow.Style.Font.Bold = true;
                    worksheet.Cell(currentRow, 6).SetValue("Total:");
                    worksheet.Cell(currentRow, 7).FormulaA1 = $"=SUM(G2:G{currentRow - 1})";
                }

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(stream.ToArray(), contentType, fileName);
                }
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

            var viewModel = new EditHorarioViewModel
            {
                Id = horario.Id,
                NomeUtilizador = horario.User?.NomeCompleto ?? horario.User?.UserName,
                Data = horario.Data,
                HoraEntrada = horario.HoraEntrada,
                SaidaAlmoco = horario.SaidaAlmoco,
                EntradaAlmoco = horario.EntradaAlmoco,
                HoraSaida = horario.HoraSaida
            };

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

                    var dataFormulario = viewModel.Data;

                    DateTime CombineAndConvertToUtc(DateTime date, DateTime timeFromForm)
                    {
                        if (timeFromForm.TimeOfDay == TimeSpan.Zero && timeFromForm.Year == 1)
                        {
                            return DateTime.MinValue.ToUniversalTime();
                        }
                        var dateTimeLocal = new DateTime(date.Year, date.Month, date.Day,
                                                        timeFromForm.Hour, timeFromForm.Minute, timeFromForm.Second,
                                                        DateTimeKind.Local);
                        return dateTimeLocal.ToUniversalTime();
                    }

                    horarioNaBd.Data = DateTime.SpecifyKind(dataFormulario.Date, DateTimeKind.Utc);
                    horarioNaBd.HoraEntrada = CombineAndConvertToUtc(dataFormulario, viewModel.HoraEntrada);
                    horarioNaBd.SaidaAlmoco = CombineAndConvertToUtc(dataFormulario, viewModel.SaidaAlmoco);
                    horarioNaBd.EntradaAlmoco = CombineAndConvertToUtc(dataFormulario, viewModel.EntradaAlmoco);
                    horarioNaBd.HoraSaida = CombineAndConvertToUtc(dataFormulario, viewModel.HoraSaida);

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