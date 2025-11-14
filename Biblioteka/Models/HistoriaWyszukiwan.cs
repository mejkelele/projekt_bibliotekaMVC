using System;
using System.ComponentModel.DataAnnotations;

namespace Biblioteka.Models
{
    public class HistoriaWyszukiwan
    {
        public int Id { get; set; }

        [Display(Name = "Użytkownik")]
        public int UserId { get; set; }

        [Display(Name = "Nazwa Wyszukiwania")]
        public string Nazwa { get; set; } = string.Empty; // Nazwa nadana przez użytkownika

        [Display(Name = "Zapytanie")]
        public string Zapytanie { get; set; } = string.Empty; // Pełne zapytanie JSON/String

        [Display(Name = "Data Zapisu")]
        public DateTime DataZapisu { get; set; } = DateTime.Now;

        // Właściwość nawigacyjna
        public User? User { get; set; }
    }
}