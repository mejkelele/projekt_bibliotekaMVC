using System.Collections.Generic;

namespace Biblioteka.Models
{
    public class HomeIndexViewModel
    {


        // Lista losowych, dostępnych książek
        public List<Ksiazka> DostepneKsiazki { get; set; } = new List<Ksiazka>();
    }
}