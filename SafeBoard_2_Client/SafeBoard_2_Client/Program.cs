using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SafeBoard_2_Client
{
    class Program
    {

        static void Main(string[] args)
        {
            //Описываем исходный порт и IP-адрес, по которомым можно подключиться к серверу
            int serverPort = 1984;
            string serverIP = "127.4.5.1";
            //Формируем конечный адрес для подключения к серверу с помощью Socket
            IPEndPoint ipPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);
            //Создаём сокет и подключаемся к серверу
            try
            {
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(ipPoint);
                //Создаём строковую переменную, в которой, в зависимости от переданных через консоль аргументов,
                //формируем запрос на сканирование или просмотр статуса задачи
                string newTask;
                switch (args[0])
                {
                    case "scan":
                        newTask = "0" + args[1];
                        break;

                    case "status":
                        newTask = "1" + args[1];
                        break;

                    default:
                        newTask = "2";
                        break;
                }

                //Переводим строку-запрос в байтовое представление и отправляем на сервер
                byte[] data = Encoding.Unicode.GetBytes(newTask);
                socket.Send(data);

                //Переменная, в которую будет записан общий размер сообщения-запроса
                int bytes = 0;
                //Буфер, для промежуточного хранения данных сообщения-ответа
                data = new byte[256];
                //Переменная, в которую будет записано полученное от сервера сообщение
                StringBuilder builder = new StringBuilder();
                //Используя буфер, записываем в builder всё переданное сообщение
                do
                {
                    bytes = socket.Receive(data, data.Length, 0);
                    builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
                }
                while (socket.Available > 0);
                //Выводим полученный ответ в консоль
                Console.WriteLine(builder.ToString());

                //Закрываем сокет и обрываем подключение
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }
            catch
            {
                Console.WriteLine("Произошла ошибка при попытке соединения с сервером");
                Console.WriteLine("Возможно введённые данные имеют некорректный формат");
            }
        }
    }
}
