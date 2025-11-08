// Models/Horario.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SIGHR.Areas.Identity.Data;

namespace SIGHR.Models
{
    /// <summary>
    /// Representa um registo diário de ponto de um utilizador.
    /// Contém todas as marcações de entrada e saída.
    /// As horas são agora guardadas como DateTime (UTC) para lidar com fusos horários.
    /// </summary>
    public class Horario
    {
        public long Id { get; set; }

        [Required(ErrorMessage = "O ID do utilizador é obrigatório.")]
        public required string UtilizadorId { get; set; }

        /// <summary>
        /// A data do registo de ponto (sem hora, ou com hora 00:00:00 UTC).
        /// </summary>
        [Required(ErrorMessage = "A data é obrigatória.")]
        public DateTime Data { get; set; }

        /// <summary>
        /// A hora de início do dia de trabalho (registada em UTC).
        /// </summary>
        [Required(ErrorMessage = "A hora de entrada é obrigatória.")]
        public DateTime HoraEntrada { get; set; } // ALTERADO DE TimeSpan PARA DateTime

        /// <summary>
        /// A hora de fim do dia de trabalho (registada em UTC).
        /// </summary>
        [Required(ErrorMessage = "A hora de saída é obrigatória.")]
        public DateTime HoraSaida { get; set; } // ALTERADO DE TimeSpan PARA DateTime

        /// <summary>
        /// A hora de regresso do almoço (registada em UTC).
        /// </summary>
        [Required(ErrorMessage = "A hora de entrada do almoço é obrigatória.")]
        public DateTime EntradaAlmoco { get; set; } // ALTERADO DE TimeSpan PARA DateTime

        /// <summary>
        /// A hora de saída para o almoço (registada em UTC).
        /// </summary>
        [Required(ErrorMessage = "A hora de saída para almoço é obrigatória.")]
        public DateTime SaidaAlmoco { get; set; } // ALTERADO DE TimeSpan PARA DateTime

        //
        // ================== INÍCIO DAS NOVAS PROPRIEDADES ==================
        //
        public double? LatitudeEntrada { get; set; }
        public double? LongitudeEntrada { get; set; }

        public double? LatitudeSaidaAlmoco { get; set; }
        public double? LongitudeSaidaAlmoco { get; set; }

        public double? LatitudeEntradaAlmoco { get; set; }
        public double? LongitudeEntradaAlmoco { get; set; }

        public double? LatitudeSaida { get; set; }
        public double? LongitudeSaida { get; set; }
        // 
        // =================== FIM DAS NOVAS PROPRIEDADES ====================
        //

        [ForeignKey("UtilizadorId")]
        public virtual SIGHRUser? User { get; set; }
    }
}