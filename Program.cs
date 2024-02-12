using Npgsql;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.WebSockets;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;

internal class Program
{
    private static string Host = "localhost";
    private static string Login = "postgres";
    private static string DBname = "Bank";
    private static string Password = "123";
    private static string Port = "5432";
    private static void Main(string[] args)
    {
        string connString = String.Format("Server={0};Username={1};Database={2};Port={3};Password={4};SSLMode=Prefer", Host, Login, DBname, Port, Password);

        using (var conn = new NpgsqlConnection(connString))

        {
            Console.Out.WriteLine("Opening connection");
            conn.Open();
        }

        string userInput = "";

        while (userInput != "2")
        {
            User user = new User();
            Console.WriteLine("Программа для подачи заявки на выписки по счету из банка.");
            Console.WriteLine("Выберите действие:");
            Console.WriteLine("1. Выполнить вход в банк.");
            Console.WriteLine("2. Выход из программы.");
            userInput = Console.ReadLine();

            switch (userInput)
            {
                case "1":
                    user.SignIn();
                    break;
                case "2":
                    Console.WriteLine("Выход из программы.");
                    break;
                default:
                    Console.WriteLine("Введена неизвестная команда.");
                    break;
            }
        }
    }


    class User
    {
        private int _id;
        private string _email;
        private bool _emailValidate;
        private string _login;
        private string _password;

        /// <summary>
        /// Функция отвечает за авторизацию пользователя
        /// </summary>
        internal void SignIn()
        {
            string connString = String.Format("Server={0};Username={1};Database={2};Port={3};Password={4};SSLMode=Prefer", Host, Login, DBname, Port, Password);

            Console.WriteLine("Введите логин:");
            string login = Console.ReadLine();
            Console.WriteLine("Введите пароль:");
            string password = Console.ReadLine();

            using (var conn = new NpgsqlConnection(connString))
            {
                List<User> users = new List<User>();
                Console.Out.WriteLine("Opening connection");
                conn.Open();
                NpgsqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT \"Login\", \"Password\"\r\n\tFROM public.\"Users\";";
                NpgsqlDataReader readerUsers = cmd.ExecuteReader();
                if (readerUsers.HasRows)
                {
                    while (readerUsers.Read())
                    {
                        User user = new User();
                        user._login = readerUsers.GetString(0);
                        user._password = readerUsers.GetString(1);
                        users.Add(user);
                    }
                }

                bool isEnter = false;
                User userAuth = new User();

                foreach (User user in users)
                {
                    if (user._login == login && user._password == EncryptPasswordBase64(password))
                    {
                        userAuth._id = user._id;
                        isEnter = true;
                        break;
                    }
                }

                if (isEnter)
                {
                    Console.WriteLine("Вход успешно выполнен.");
                    EnterTheBank(userAuth._id);
                }
                else Console.WriteLine("Неверная пара логин пароль.");
            }
        }

        /// <summary>
        /// Функция меню входа в банк
        /// </summary>
        /// <param name="userID">Идентификаторо пользователя</param>
        private void EnterTheBank(int userID)
        {
            string userInput = "";

            while (userInput != "2")
            {
                User user = new User();
                Console.WriteLine("1. Подать новую заявку.");
                Console.WriteLine("2. Выход из аккаунта.");

                userInput = Console.ReadLine();

                switch (userInput)
                {
                    case "1":
                        AddNewRequest(userID);
                        break;
                    case "2":
                        Console.WriteLine("Выход из аккаунта.");
                        break;
                    default:
                        Console.WriteLine("Введена неизвестная команда.");
                        break;
                }
            }
        }

        /// <summary>
        /// Функция подает заявку на выписки по счету авторизованного пользователя
        /// </summary>
        /// <param name="userID"></param>
        internal void AddNewRequest(int userID)
        {
            DateTime dateTimeNewRequest = DateTime.Now;
            string connString = String.Format("Server={0};Username={1};Database={2};Port={3};Password={4};SSLMode=Prefer", Host, Login, DBname, Port, Password);

            Console.WriteLine("Введите свой Email-адрес:");
            string emailString = Console.ReadLine();

            while (!EmailisValid(emailString))
            {
                Console.WriteLine("Введен неверный email-адрес");
                emailString = Console.ReadLine();
            }

            using (var conn = new NpgsqlConnection(connString))
            {
                List<User> users = new List<User>();
                Console.Out.WriteLine("Opening connection");
                conn.Open();
                NpgsqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE \"Users\" \r\n\tSET Email = " + emailString + " WHERE \"ID\" = " + userID + ";";
                NpgsqlDataReader readerUsers = cmd.ExecuteReader();
            }

            Console.WriteLine("Заявка на выписки на почту " + emailString + " выполнена.");

            GetAllStatementsForUser(userID, dateTimeNewRequest) ;
        }

        bool EmailisValid(string email)
        {
            string pattern = "[.\\-_a-z0-9]+@([a-z0-9][\\-a-z0-9]+\\.)+[a-z]{2,6}";
            Match isMatch = Regex.Match(email, pattern, RegexOptions.IgnoreCase);
            return isMatch.Success;
        }

        /// <summary>
        /// Функция получает все выписки по счету по пользователю
        /// </summary>
        /// <param name="userID">Идентификатор пользователя</param>
        /// <param name="dateTimeNewRequest">Дата и время подачи заявки</param>
        /// <returns></returns>
        internal async Task GetAllStatementsForUser(int userID, DateTime dateTimeNewRequest)
        {
            string connString = String.Format("Server={0};Username={1};Database={2};Port={3};Password={4};SSLMode=Prefer", Host, Login, DBname, Port, Password);
            bool isEmailConfirmed = false;

            using (var conn = new NpgsqlConnection(connString))
            {
                Console.Out.WriteLine("Opening connection");
                conn.Open();
                NpgsqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT \"EmailValidate\"\r\n\tFROM public.\"Users\"\r\n\twhere \"ID\" = " + userID + ";";
                NpgsqlDataReader readerUsers = cmd.ExecuteReader();
                if (readerUsers.HasRows)
                {
                    while (readerUsers.Read())
                    {
                        isEmailConfirmed = readerUsers.GetBoolean(0);
                    }
                }
            }

            if (isEmailConfirmed == true)
            {
                List<string> statements = new List<string>();
                string data = "";

                using (var conn = new NpgsqlConnection(connString))
                {
                    Console.Out.WriteLine("Opening connection");
                    conn.Open();
                    NpgsqlCommand cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT \"Data\"\r\n\tFROM public.\"Statements\"\r\n\twhere \"UserID\" = " + userID + ";";
                    NpgsqlDataReader readerStatements = cmd.ExecuteReader();
                    if (readerStatements.HasRows)
                    {
                        while (readerStatements.Read())
                        {
                            data = readerStatements.GetString(0);
                            statements.Add(data);
                        }
                    }
                }

                int indexStatements = 1;

                foreach (string s in statements)
                {
                    string path = "D://statement" + indexStatements + ".txt";
                    string text = data;
                    // полная перезапись файла 
                    using (StreamWriter writer = new StreamWriter(path, false))
                    {
                        await writer.WriteLineAsync(text);
                    }
                }

                //Отправка письма со всеми выписками по счету по пользователю
                MailAddress from = new MailAddress("somemail@gmail.com", "Банк");
                // кому отправляем
                MailAddress to = new MailAddress("somemail@yandex.ru");
                // создаем объект сообщения
                MailMessage m = new MailMessage(from, to);
                // тема письма
                m.Subject = "Выписки по вашему счету";
                // текст письма
                m.Body = "<h2>Письмо</h2>";
                // письмо представляет код html
                m.IsBodyHtml = true;

                foreach (string s in statements)
                {
                    m.Attachments.Add(new Attachment("D://statement" + indexStatements + ".txt"));
                }

                // адрес smtp-сервера и порт, с которого будем отправлять письмо
                SmtpClient smtp = new SmtpClient("smtp.gmail.com", 587);
                // логин и пароль
                smtp.Credentials = new NetworkCredential("somemail@gmail.com", "mypassword");
                smtp.EnableSsl = true;
                smtp.Send(m);
            }
            else
            {
                ConfirmEmail(userID, dateTimeNewRequest);
            }
        }

        /// <summary>
        /// Функция отправляет письмо с подтверждением Email каждый день в течение 30 дней, пока Email не будет подтвержден
        /// </summary>
        /// <param name="userID">Идентификатор пользователя</param>
        /// <param name="dateTimeNewRequest">Дата и время подачи заявки</param>
        /// <returns></returns>
        internal async Task ConfirmEmail(int userID, DateTime dateTimeNewRequest)
        {
            DateTime dateTimeFixed = DateTime.Now;
            string connString = String.Format("Server={0};Username={1};Database={2};Port={3};Password={4};SSLMode=Prefer", Host, Login, DBname, Port, Password);
            User user = new User();

            string userIPAddress = "192.168.0.1";

            //Если IP-адрес пользователя и дата и время не больше 10 минут, то Email подтверждаем
            if(userIPAddress == "IP-адрес пользователя" && dateTimeNewRequest.AddMinutes(10) > DateTime.Now)
            {
                using (var conn = new NpgsqlConnection(connString))
                {
                    List<User> users = new List<User>();
                    Console.Out.WriteLine("Opening connection");
                    conn.Open();
                    NpgsqlCommand cmd = conn.CreateCommand();
                    cmd.CommandText = "UPDATE \"Users\" \r\n\tSET EmailValidate = 1 WHERE \"ID\" = " + userID + ";";
                    NpgsqlDataReader readerUsers = cmd.ExecuteReader();
                }
            }

            using (var conn = new NpgsqlConnection(connString))
            {
                Console.Out.WriteLine("Opening connection");
                conn.Open();
                NpgsqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT \"EmailValidate\"\r\n\tFROM public.\"Users\"\r\n\twhere \"ID\" = " + userID + ";";
                NpgsqlDataReader readerUsers = cmd.ExecuteReader();
                if (readerUsers.HasRows)
                {
                    while (readerUsers.Read())
                    {
                        user._emailValidate = readerUsers.GetBoolean(0);
                    }
                }
            }

            //Пока Email остается не подтвержденным и со времени подачи заяки не прошло 30 дней отправляем письмо с подтверждением
            while (user._emailValidate == false || DateTime.Now < dateTimeNewRequest.AddDays(30))
            {
                // наш email с заголовком письма
                MailAddress from = new MailAddress("somemail@yandex.ru", "Web Registration");
                // кому отправляем
                MailAddress to = new MailAddress(user._email);
                // создаем объект сообщения
                MailMessage m = new MailMessage(from, to);
                // тема письма
                m.Subject = "Email confirmation";
                // текст письма - включаем в него ссылку
                m.Body = string.Format("Для завершения регистрации перейдите по ссылке:" +
                                "<a href=\"{0}\" title=\"Подтвердить регистрацию\">{0}</a>"/*,
                    Url.Action("ConfirmEmail", "Account", new { Token = user.Id, Email = user.Email }, Request.Url.Scheme)*/);
                m.IsBodyHtml = true;
                // адрес smtp-сервера, с которого мы и будем отправлять письмо
                SmtpClient smtp = new System.Net.Mail.SmtpClient("smtp.yandex.ru", 25);
                // логин и пароль
                smtp.Credentials = new System.Net.NetworkCredential("somemail@yandex.ru", "password");
                smtp.Send(m);

                await Task.Delay(86400000);
            }
        }

        /// <summary>
        /// Функция шифрует пароль пользователя
        /// </summary>
        /// <param name="password">Пароль пользователя</param>
        /// <returns></returns>
        private static string EncryptPasswordBase64(string password)
        {
            var textBytes = System.Text.Encoding.UTF8.GetBytes(password);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(password));
        }

        /// <summary>
        /// Функция дешифрует пароль пользователя
        /// </summary>
        /// <param name="base64EncodedData">Зашифрованный пароль пользователя</param>
        /// <returns></returns>
        private static string DecryptPasswordBase64(string base64EncodedData)
        {
            var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }
    }
}