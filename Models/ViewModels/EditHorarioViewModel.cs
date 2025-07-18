// Models/ViewModels/EditHorarioViewModel.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace SIGHR.Models.ViewModels
{
    /// <summary>
    /// ViewModel para o formulário de edição de um registo de horário na área de administração.
    /// Permite a edição das horas de ponto e, opcionalmente, a data.
    /// </summary>
    public class EditHorarioViewModel
    {
        public long Id { get; set; } // ID do Horário a ser editado

        [Display(Name = "Utilizador")]
        public string? NomeUtilizador { get; set; } // Apenas para exibição, não é editável

        [Required(ErrorMessage = "A data é obrigatória.")]
        [DataType(DataType.Date)]
        [Display(Name = "Data")]
        public DateTime Data { get; set; }

        [Required(ErrorMessage = "A hora de entrada é obrigatória.")]
        [DataType(DataType.Time)]
        [Display(Name = "Hora de Entrada")]
        public TimeSpan HoraEntrada { get; set; }

        [Display(Name = "Saída Almoço")]
        [DataType(DataType.Time)]
        public TimeSpan SaidaAlmoco { get; set; } // Pode ser TimeSpan.Zero

        [Display(Name = "Entrada Almoço")]
        [DataType(DataType.Time)]
        public TimeSpan EntradaAlmoco { get; set; } // Pode ser TimeSpan.Zero

        [Display(Name = "Saída")]
        [DataType(DataType.Time)]
        public TimeSpan HoraSaida { get; set; } // Pode ser TimeSpan.Zero
    }
}