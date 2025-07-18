// Controllers/Api/HorariosApiController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIGHR.Areas.Identity.Data;
using SIGHR.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System; // Adicionado para DateTime
using Microsoft.Extensions.Logging;

namespace SIGHR.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class HorariosApiController : ControllerBase
    {
        private readonly SIGHRContext _context;
        private readonly ILogger<HorariosApiController> _logger;

        public HorariosApiController(SIGHRContext context, ILogger<HorariosApiController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Endpoint para obter a lista de todos os horários.
        /// As horas são agora DateTime.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Horario>>> Get() => await _context.Horarios.ToListAsync();

        /// <summary>
        /// Endpoint para obter um horário específico pelo ID.
        /// As horas são agora DateTime.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<Horario>> Get(long id)
        {
            var item = await _context.Horarios.FindAsync(id);
            return item == null ? NotFound() : item;
        }

        /// <summary>
        /// Endpoint para atualizar os dados de um horário existente.
        /// As horas são agora DateTime.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(long id, Horario model)
        {
            if (id != model.Id) return BadRequest();

            // Assegurar que as horas recebidas são tratadas como UTC se vierem sem Kind.
            // Para TimeSpans convertidos para DateTime, a data pode ser MinValue.
            if (model.HoraEntrada.Kind == DateTimeKind.Unspecified) model.HoraEntrada = DateTime.SpecifyKind(model.HoraEntrada, DateTimeKind.Utc);
            if (model.HoraSaida.Kind == DateTimeKind.Unspecified) model.HoraSaida = DateTime.SpecifyKind(model.HoraSaida, DateTimeKind.Utc);
            if (model.EntradaAlmoco.Kind == DateTimeKind.Unspecified) model.EntradaAlmoco = DateTime.SpecifyKind(model.EntradaAlmoco, DateTimeKind.Utc);
            if (model.SaidaAlmoco.Kind == DateTimeKind.Unspecified) model.SaidaAlmoco = DateTime.SpecifyKind(model.SaidaAlmoco, DateTimeKind.Utc);
            if (model.Data.Kind == DateTimeKind.Unspecified) model.Data = DateTime.SpecifyKind(model.Data, DateTimeKind.Utc); // A data completa do registo

            _context.Entry(model).State = EntityState.Modified;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Horarios.Any(e => e.Id == id)) return NotFound();
                else throw;
            }
            return NoContent();
        }

        /// <summary>
        /// Endpoint para criar um novo horário.
        /// As horas são agora DateTime.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<Horario>> Post(Horario model)
        {
            // Assegurar que as horas recebidas são tratadas como UTC se vierem sem Kind.
            if (model.HoraEntrada.Kind == DateTimeKind.Unspecified) model.HoraEntrada = DateTime.SpecifyKind(model.HoraEntrada, DateTimeKind.Utc);
            if (model.HoraSaida.Kind == DateTimeKind.Unspecified) model.HoraSaida = DateTime.SpecifyKind(model.HoraSaida, DateTimeKind.Utc);
            if (model.EntradaAlmoco.Kind == DateTimeKind.Unspecified) model.EntradaAlmoco = DateTime.SpecifyKind(model.EntradaAlmoco, DateTimeKind.Utc);
            if (model.SaidaAlmoco.Kind == DateTimeKind.Unspecified) model.SaidaAlmoco = DateTime.SpecifyKind(model.SaidaAlmoco, DateTimeKind.Utc);
            if (model.Data.Kind == DateTimeKind.Unspecified) model.Data = DateTime.SpecifyKind(model.Data, DateTimeKind.Utc); // A data completa do registo

            _context.Horarios.Add(model);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = model.Id }, model);
        }

        /// <summary>
        /// Endpoint para remover um horário existente.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var item = await _context.Horarios.FindAsync(id);
            if (item == null) return NotFound();
            _context.Horarios.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}