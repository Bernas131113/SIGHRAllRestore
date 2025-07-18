// Models/ViewModels/EditHorarioViewModel.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace SIGHR.Models.ViewModels
{
    public class EditHorarioViewModel
    {
        public long Id { get; set; }

        [Display(Name = "Utilizador")]
        public string? NomeUtilizador { get; set; }

        [Required(ErrorMessage = "A data é obrigatória.")]
        [DataType(DataType.Date)]
        public DateTime Data { get; set; }

        [Required(ErrorMessage = "A hora de entrada é obrigatória.")]
        [DataType(DataType.Time)]
        [Display(Name = "Hora de Entrada")]
        public DateTime HoraEntrada { get; set; } // <<-- ALTERADO PARA DateTime

        [DataType(DataType.Time)]
        [Display(Name = "Saída Almoço")]
        public DateTime SaidaAlmoco { get; set; } // <<-- ALTERADO PARA DateTime

        [DataType(DataType.Time)]
        [Display(Name = "Entrada Almoço")]
        public DateTime EntradaAlmoco { get; set; } // <<-- ALTERADO PARA DateTime

        [DataType(DataType.Time)]
        [Display(Name = "Saída")]
        public DateTime HoraSaida { get; set; } // <<-- ALTERADO PARA DateTime
    }
}