using System;

namespace Domain.Modeli
{
    [Serializable]
    public class Korisnik
    {
        public string ImePrezime { get; set; }
        public string Nickname { get; set; }
        public string Password { get; set; }
        public bool Login { get; set; } = false;
        public int UDPPort { get; set; } = 0;
        public bool Prijavljen { get; set; } = false;
        public Korisnik () { Nickname = "Unknown"; Password = "Unknown"; Login = false; }

        public Korisnik(string nick, string pw, bool login)
        {
            Nickname = nick; 
            Password = pw;
            Login = login;
        }
        public Korisnik(string imePrezime, string nickname, string password, bool login)
        {
            ImePrezime = imePrezime;
            Nickname = nickname;
            Password = password;
            Login = login;
        }
    }
}
