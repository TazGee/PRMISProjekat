namespace Domain.Modeli
{
    public class Korisnik
    {
        long Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Nickname { get; set; }
        public string Password { get; set; }
        public bool Login {  get; set; }

        public Korisnik () { Nickname = "Unknown"; Password = "Unknown"; Login = false; }

        public Korisnik(string nick, string pw, bool login)
        {
            Nickname = nick; 
            Password = pw;
            Login = login;
        }
        public Korisnik(long id, string firstName, string lastName, string nickname, string password, bool login)
        {
            Id = id;
            FirstName = firstName;
            LastName = lastName;
            Nickname = nickname;
            Password = password;
            Login = login;
        }
    }
}
