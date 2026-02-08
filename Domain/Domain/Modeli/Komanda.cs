using Domain.Enumeratori;
using System;

namespace Domain.Modeli
{
    [Serializable]
    public struct Komanda
    {
        public TipKomande tipKomande;
        public RezultatKomande rezultatKomande;
        public string dodatnaPoruka;
        public long idKorisnika;
    }
}
