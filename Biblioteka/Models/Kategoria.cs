using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Biblioteka.Models
{
    public class Kategoria
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Nazwa kategorii jest wymagana.")]
        [Display(Name = "Nazwa Kategorii")]
        public string Nazwa { get; set; } = string.Empty;

        // Klucz obcy wskazujący na kategorię nadrzędną (Parent). 
        // Wartość null oznacza, że jest to kategoria główna/korzeń drzewa.
        [Display(Name = "Kategoria nadrzędna")]
        public int? ParentId { get; set; }

        // Właściwość nawigacyjna do kategorii nadrzędnej (Parent)
        public Kategoria? Parent { get; set; }

        // Właściwość nawigacyjna do podkategorii (Children)
        public ICollection<Kategoria> Children { get; set; } = new List<Kategoria>();

        // Właściwość nawigacyjna do Książek
        public ICollection<Ksiazka> Ksiazki { get; set; } = new List<Ksiazka>();
    }
}