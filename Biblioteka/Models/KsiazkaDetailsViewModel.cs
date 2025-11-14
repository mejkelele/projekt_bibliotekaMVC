using System.Collections.Generic;

namespace Biblioteka.Models
{
    public class KsiazkaDetailsViewModel
    {
        public Ksiazka Ksiazka { get; set; } = new Ksiazka();
        public List<Rezerwacja> AktywneRezerwacje { get; set; } = new List<Rezerwacja>();
    }
}