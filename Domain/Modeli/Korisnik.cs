namespace Domain.Modeli
{
    public class Korisnik
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Nickname { get; set; }
        public string Password { get; set; }
        public bool Login {  get; set; }
        public int UDPPort { get; set; } = 0;

        public Korisnik () { Nickname = "Unknown"; Password = "Unknown"; Login = false; }

        public Korisnik(string nick, string pw, bool login)
        {
            Nickname = nick; 
            Password = pw;
            Login = login;
        }
        public Korisnik(string firstName, string lastName, string nickname, string password, bool login)
        {
            FirstName = firstName;
            LastName = lastName;
            Nickname = nickname;
            Password = password;
            Login = login;
        }
    }
}
