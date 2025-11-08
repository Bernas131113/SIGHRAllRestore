// Models/ViewModels/HorarioAdminViewModel.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace SIGHR.Models.ViewModels
{
    /// <summary>
    /// ViewModel para apresentar os detalhes de um registo de ponto na área de administração.
    /// As horas são agora DateTime.
    /// </summary>
    public class HorarioAdminViewModel
    {
        public long HorarioId { get; set; }

        [Display(Name = "Utilizador")]
        public string? NomeUtilizador { get; set; }

        [Display(Name = "Data")]
        [DataType(DataType.Date)]
        public DateTime Data { get; set; }

        /// <summary>
        /// A hora de início do dia de trabalho.
        /// </summary>
        [Display(Name = "Hora de Entrada")]
        [DataType(DataType.Time)] // Usamos DataType.Time para hints da UI
        public DateTime HoraEntrada { get; set; } // ALTERADO DE TimeSpan PARA DateTime

        /// <summary>
        /// A hora de saída para o almoço.
        /// </summary>
        [Display(Name = "Saída Almoço")]
        [DataType(DataType.Time)] // Usamos DataType.Time para hints da UI
        public DateTime SaidaAlmoco { get; set; } // ALTERADO DE TimeSpan PARA DateTime

        /// <summary>
        /// A hora de regresso do almoço.
        /// </summary>
        [Display(Name = "Entrada Almoço")]
        [DataType(DataType.Time)] // Usamos DataType.Time para hints da UI
        public DateTime EntradaAlmoco { get; set; } // ALTERADO DE TimeSpan PARA DateTime

        /// <summary>
        /// A hora de fim do dia de trabalho.
        /// </summary>
        [Display(Name = "Saída")]
        [DataType(DataType.Time)] // Usamos DataType.Time para hints da UI
        public DateTime HoraSaida { get; set; } // ALTERADO DE TimeSpan PARA DateTime

        [Display(Name = "Total de Horas")]
        public string? TotalHorasTrabalhadas { get; set; }

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
    }
}