using System;

namespace Domain.Modeli
{
    [Serializable]
    public class Korisnik
    {
        public long Id { get; set; }
        public string ImePrezime { get; set; } = "Nepoznato";
        public string Nickname { get; set; }
        public string Password { get; set; }
        public bool Login { get; set; } = false;
        public bool Prijavljen { get; set; } = false;
        public int UDPPort { get; set; }
        public Korisnik () { Nickname = "Unknown"; Password = "Unknown"; Login = false; }

        public Korisnik(string nick, string pw, bool login)
        {
            Id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Nickname = nick; 
            Password = pw;
            Login = login;
        }
        public Korisnik(string imePrezime, string nickname, string password, bool login)
        {
            Id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            ImePrezime = imePrezime;
            Nickname = nickname;
            Password = password;
            Login = login;
        }

        public string GetProperties()
        {
            return ($"{Nickname} | {ImePrezime} | UDP Port: {UDPPort}");
        }
    }
}
