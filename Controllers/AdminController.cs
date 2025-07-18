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
                    worksheet.Cell(currentRow, 2).SetValue(item.Data.ToLocalTime()).Style.NumberFormat.Format = "dd/MM/yyyy";
                    if (item.HoraEntrada != DateTime.MinValue.ToUniversalTime()) worksheet.Cell(currentRow, 3).SetValue(item.HoraEntrada.ToLocalTime()).Style.NumberFormat.Format = "HH:mm";
                    if (item.SaidaAlmoco != DateTime.MinValue.ToUniversalTime()) worksheet.Cell(currentRow, 4).SetValue(item.SaidaAlmoco.ToLocalTime()).Style.NumberFormat.Format = "HH:mm";
                    if (item.EntradaAlmoco != DateTime.MinValue.ToUniversalTime()) worksheet.Cell(currentRow, 5).SetValue(item.EntradaAlmoco.ToLocalTime()).Style.NumberFormat.Format = "HH:mm";
                    if (item.HoraSaida != DateTime.MinValue.ToUniversalTime()) worksheet.Cell(currentRow, 6).SetValue(item.HoraSaida.ToLocalTime()).Style.NumberFormat.Format = "HH:mm";
                    TimeSpan totalTrabalhadoExcel = TimeSpan.Zero;
                    if (item.HoraEntrada != DateTime.MinValue.ToUniversalTime() && item.HoraSaida != DateTime.MinValue.ToUniversalTime())
                    {
                        TimeSpan tempoAlmocoExcel = TimeSpan.Zero;
                        TimeSpan horaEntradaTsExcel = item.HoraEntrada.TimeOfDay;
                        TimeSpan saidaAlmocoTsExcel = item.SaidaAlmoco.TimeOfDay;
                        TimeSpan entradaAlmocoTsExcel = item.EntradaAlmoco.TimeOfDay;
                        TimeSpan horaSaidaTsExcel = item.HoraSaida.TimeOfDay;
                        if (entradaAlmocoTsExcel > saidaAlmocoTsExcel && saidaAlmocoTsExcel != TimeSpan.Zero) tempoAlmocoExcel = entradaAlmocoTsExcel - saidaAlmocoTsExcel;
                        if (horaSaidaTsExcel > horaEntradaTsExcel) { totalTrabalhadoExcel = (horaSaidaTsExcel - horaEntradaTsExcel) - tempoAlmocoExcel; if (totalTrabalhadoExcel < TimeSpan.Zero) totalTrabalhadoExcel = TimeSpan.Zero; }
                    }
                    if (totalTrabalhadoExcel > TimeSpan.Zero) worksheet.Cell(currentRow, 7).SetValue(totalTrabalhadoExcel).Style.NumberFormat.Format = "[h]:mm";
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

            // ---- CORREÇÃO APLICADA AQUI ----
            // Só converte para ToLocalTime() se a data não for a data "zero" (MinValue).
            var viewModel = new EditHorarioViewModel
            {
                Id = horario.Id,
                NomeUtilizador = horario.User?.NomeCompleto ?? horario.User?.UserName,
                Data = horario.Data.ToLocalTime(), // A data principal do registo deve ser sempre válida.

                HoraEntrada = horario.HoraEntrada.Year == 1 ? horario.HoraEntrada : horario.HoraEntrada.ToLocalTime(),
                SaidaAlmoco = horario.SaidaAlmoco.Year == 1 ? horario.SaidaAlmoco : horario.SaidaAlmoco.ToLocalTime(),
                EntradaAlmoco = horario.EntradaAlmoco.Year == 1 ? horario.EntradaAlmoco : horario.EntradaAlmoco.ToLocalTime(),
                HoraSaida = horario.HoraSaida.Year == 1 ? horario.HoraSaida : horario.HoraSaida.ToLocalTime()
            };
            // ---------------------------------

            ViewData["Title"] = $"Editar Registo de {viewModel.Data:dd/MM/yyyy}";
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

                    // ---- CORREÇÃO APLICADA AQUI ----
                    // Lógica robusta para combinar data e hora, respeitando valores "zero".
                    var dataLocal = viewModel.Data.Date; // Apenas a parte da data

                    // Função auxiliar para combinar e converter
                    DateTime CombineAndConvertToUtc(DateTime date, DateTime time)
                    {
                        // Se a hora do input for MinValue (ex: 00:00:00), guarda MinValue UTC
                        if (time.TimeOfDay == TimeSpan.Zero)
                        {
                            return DateTime.MinValue.ToUniversalTime();
                        }
                        // Combina a data local com a hora local e converte para UTC
                        return date.Add(time.TimeOfDay).ToUniversalTime();
                    }

                    horarioNaBd.Data = dataLocal.ToUniversalTime();
                    horarioNaBd.HoraEntrada = CombineAndConvertToUtc(dataLocal, viewModel.HoraEntrada);
                    horarioNaBd.SaidaAlmoco = CombineAndConvertToUtc(dataLocal, viewModel.SaidaAlmoco);
                    horarioNaBd.EntradaAlmoco = CombineAndConvertToUtc(dataLocal, viewModel.EntradaAlmoco);
                    horarioNaBd.HoraSaida = CombineAndConvertToUtc(dataLocal, viewModel.HoraSaida);
                    // ---------------------------------

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