namespace Domain.Modeli
{
    public class Korisnik
    {
        long Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Nickname { get; set; }
        public string Password { get; set; }
        public int Port { get; set; }

        public Korisnik () { Nickname = "Unknown"; Password = "Unknown"; }

        public Korisnik(string nick, string pw)
        {
            Nickname = nick; 
            Password = pw;
        }
    }
}
