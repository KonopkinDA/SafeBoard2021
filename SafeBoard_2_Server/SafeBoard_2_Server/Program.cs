using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SafeBoard_2_Server
{
    //Описываем класс, инкапсулирующий создаваемую задачу на сканирование, и её результаты
    public class ScanTask
    {
        //ID конкретной задачи
        public int taskID;
        //Путь к директории, в которой будет производиться сканирование
        public string taskPath;
        //Переменная-флаг, позволяющая понять, закончено ли сканирование
        public bool isEnd;
        //Описываем несколько переменных-счётчиков
        //В errorCount_1-3 будут записываться случаи нахождения каждого из трёх типов подозрительных файлов
        //В errorCount_A будут записаны все случаи отказа в доступе к папкам или файлам
        //В fileCount будет записано общее колличество успешно просканированных файлов
        public int errorCount_1, errorCount_2, errorCount_3, errorCount_A, fileCount;
        //Переменная, в которую будет записано общее время сканирования по конкретной задаче
        public TimeSpan taskTime;

        //Конструкторы, для корректного создания новых экземпляров задач сканирования
        public ScanTask()
        {
            taskID = 0;
            taskPath = "";
            isEnd = false;
            fileCount = 0;
            errorCount_1 = 0;
            errorCount_2 = 0;
            errorCount_3 = 0;
            errorCount_A = 0;
            taskTime = TimeSpan.Zero;
        }
        public ScanTask(int _taskID, string _taskPath)
        {
            taskID = _taskID;
            taskPath = _taskPath;
            isEnd = false;
            fileCount = 0;
            errorCount_1 = 0;
            errorCount_2 = 0;
            errorCount_3 = 0;
            errorCount_A = 0;
            taskTime = TimeSpan.Zero;
        }
    }

    class Program
    {
        //Переменная-счётчик, хранящая в себе текущий свободный ID
        public static int scanID = 0;
        //Лист, хранящий в себе все принятые сервером задания на сканирование, завершённые, и ещё находящиеся в процессе
        public static List<ScanTask> scanTasks = new List<ScanTask>();
        static void Main(string[] args)
        {
            //Запускаем работу сервера в отдельном потоке, возвращая управление в командную строку
            //Блягодаря этому, пользователь может продолжить работу в командной строке, из под которой был запущен сервер
            Thread t = new Thread(new ThreadStart(Server));
            t.Start();
            //Стартуем новый процесс командной строки, в которой пользователь продолжит работу
            Process p = new Process();
            p.StartInfo.FileName = "cmd.exe";
            p.Start();
        }

        //Основная функция, реализующая функционал сервера, принимающего запросы на создание задач на сканирование
        //и просмотр их статуса
        public static void Server()
        {
            //Описываем исходный порт и IP-адрес, по которомым к серверу можно будет подключиться
            int serverPort = 1984;
            string serverIP = "127.4.5.1";

            //Формируем конечный адрес для подключения к серверу с помощью Socket
            IPEndPoint serverPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);

            //Создаём сокет
            Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                //Связываем созданный сокет с адресом, по которому сервер будет принимать запросы
                listenSocket.Bind(serverPoint);

                //Включаем режим "прослушивания" запросов на подключение
                listenSocket.Listen(10);
                Console.WriteLine("Сервер сканирования запущен");

                //Запускаем бесконечный цикл работы сервера
                while (true)
                {
                    //Создаём вспомогательный сокет для приёма запроса
                    Socket handler = listenSocket.Accept();

                    //Переменная, в которую будет записан общий размер сообщения-запроса
                    int bytesCount;
                    //Буфер, для промежуточного хранения данных сообщения-запроса
                    byte[] receivedData = new byte[256];
                    //Переменная, в которую будет записано переданное серверу сообщение
                    StringBuilder builder = new StringBuilder();
                    //Используя буфер, записываем в builder всё переданное сообщение
                    do
                    {
                        bytesCount = handler.Receive(receivedData);
                        builder.Append(Encoding.Unicode.GetString(receivedData, 0, bytesCount));
                    }
                    while (handler.Available > 0);

                    //Создаём временную переменную типа string и записываем сообщение туда
                    string tempTask = builder.ToString();
                    //В зависимости от первого, "управляющего" символа сообщения, определяем тип запроса,
                    //и обрабатываем его в конструкции switch
                    try
                    {
                        switch (tempTask[0])
                        {
                            //Управляющий символ "0" характерен для запроса на сканирование
                            case '0':
                                //Создаём новый экземпляр задачи, присваиваем ему первый из свободных ID, пропуская нулевой,
                                //и записываем в соответствующее поле экземпляра, путь до сканируемой директории
                                scanID++;
                                //Исключаем управляющий символ из строки сообщения
                                string path = tempTask.Remove(0, 1);
                                scanTasks.Add(new ScanTask(scanID, path));
                                //Запускаем асинхронную функцию сканирования для только что созданного запроса
                                ScanAcync(scanTasks[scanTasks.Count - 1]);
                                //Формируем сообщение-ответ, которое будет передано отправившей запрос программе,
                                //переводим его в байтовое выражение и отправляем, после чего прерываем соединение
                                string serverAnswer = "Создана задача на скинрование с ID: " + scanID.ToString();
                                receivedData = Encoding.Unicode.GetBytes(serverAnswer);
                                handler.Send(receivedData);
                                handler.Shutdown(SocketShutdown.Both);
                                handler.Close();
                                break;
                            //Управляющий символ "1" характерен для запроса на просмотр статуса существующего сканирования
                            case '1':
                                //Исключаем управляющий символ из строки сообщения, и ищем задачу с полученным ID
                                //в листе существующих задач
                                tempTask = tempTask.Remove(0, 1);
                                int currentID = Convert.ToInt32(tempTask);
                                ScanTask currentST = scanTasks.Find(x => x.taskID == currentID);

                                //Если искомая задача найдена, проверяем её статус
                                if (currentST != null)
                                {
                                    //Если сканирование по задаче завершено, отправляем отчёт о нём вызывающей программе
                                    if (currentST.isEnd)
                                    {
                                        //Послать отчёт о сканировании
                                        serverAnswer = "Результаты сканирования: \n" +
                                            "Просканированно файлов: " + currentST.fileCount.ToString() + "\n" +
                                            "Обнаружено JS: " + currentST.errorCount_1.ToString() + "\n" +
                                            "Обнаружено rm -rf: " + currentST.errorCount_2.ToString() + "\n" +
                                            "Обнаружено Rundll32: " + currentST.errorCount_3.ToString() + "\n" +
                                            "Ошибки доступа: " + currentST.errorCount_A.ToString() + "\n" +
                                            "Время сканирования: " + currentST.taskTime.ToString() + "\n";
                                        receivedData = Encoding.Unicode.GetBytes(serverAnswer);
                                        handler.Send(receivedData);
                                        handler.Shutdown(SocketShutdown.Both);
                                        handler.Close();
                                    }
                                    //Иначе, отправляем сообщение о продолжающемся процессе сканирования
                                    else
                                    {
                                        serverAnswer = "Сканирование, запущенное по заданному ID находится в процессе";
                                        receivedData = Encoding.Unicode.GetBytes(serverAnswer);
                                        handler.Send(receivedData);
                                        handler.Shutdown(SocketShutdown.Both);
                                        handler.Close();
                                    }
                                }
                                //Если нет - отправляем соответствующее сообщение
                                else
                                {
                                    serverAnswer = "Задачи с указанным ID не существует";
                                    receivedData = Encoding.Unicode.GetBytes(serverAnswer);
                                    handler.Send(receivedData);
                                    handler.Shutdown(SocketShutdown.Both);
                                    handler.Close();
                                }
                                break;
                            //В случае некоректного управляющего символа, отправляем соответствующее сообщение
                            default:
                                serverAnswer = "Неверный запрос";
                                receivedData = Encoding.Unicode.GetBytes(serverAnswer);
                                handler.Send(receivedData);
                                break;
                        }
                    }
                    catch
                    {
                        string serverAnswer = "Неверный запрос";
                        receivedData = Encoding.Unicode.GetBytes(serverAnswer);
                        handler.Send(receivedData);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                //При незапланированном разрыве соединения, выводим информацию о соответствующей ошибке
                Console.WriteLine(ex.Message);
            }
        }

        //Асинхронная функция, запускающая процесс сканирования
        public static async void ScanAcync(ScanTask st)
        {
            await Task.Run(() => Scan(st));
        }

        //Функция, сканирующая директорию, переданную в соответствующем поле параметра sResult
        //и записывающая в параметр результаты сканирования
        static void Scan(ScanTask sResult)
        {
            DateTime _t = DateTime.UtcNow;

            //Используем конструкцию try-catch для того, что бы отловить возможные ошибки
            //Например, исключаем возможность того, что программе не будут переданы необходимые данные в качестве входных аргументов
            try
            {
                //Вызываем функцию сканирования директории, путь к которой передан через командную строку
                SearchDirectories(sResult.taskPath, sResult);
                //Записываем полное время сканирования
                sResult.taskTime = DateTime.UtcNow - _t;
                //Обновляем статус задачи на "завершённый"
                sResult.isEnd = true;
            }
            catch
            {
                //При возможной ошибке, делающей сканирование невозможным, удаляем задачу из общего листа
                scanTasks.Remove(sResult);
            }
        }

        //Рекурсивная функция, сканирующая все файлы в директории, лежащей по адресу path и вызывающая себя
        //для всех найденных в ней поддиректорий. Использует переданный объект класса ScanTask для записи результата
        public static void SearchDirectories(string path, ScanTask sResult)
        {
            //Используем несколько вложенных конструкций try-catch для того, что бы обнаружить случаи отказа
            //в доступе к файлам или папкам и увеличить соответствующий счётчик подобных ошибок
            try
            {
                string[] s1 = Directory.GetFiles(path);
                sResult.fileCount += s1.Length;

                //Используем функцию Parallel.ForEach для параллельного перебора всех файлов в директории
                Parallel.ForEach(s1, t1 => {
                    try
                    {
                        //Отдельно рассматриваем случаи нахождения файлов с расширением js
                        if (t1.EndsWith(".js"))
                        {
                            //Так как по условию задания, в каждом файле может присутствовать только один тип "подозрительного" содержимого,
                            //используем конструкцию из вложенных if-else
                            using (StreamReader sr = new StreamReader(t1))
                            {
                                string c = sr.ReadToEnd();
                                //Проверяем вхождение искомой "подозрительной" строчки методом String.Contains
                                if (c.Contains("<script>evil_script()</script>"))
                                {
                                    sResult.errorCount_1++;
                                }
                                else
                                {
                                    if (c.Contains(@"rm -rf %userprofile%\Documents"))
                                    {
                                        sResult.errorCount_2++;
                                    }
                                    else
                                    {
                                        if (c.Contains("Rundll32 sus.dll SusEntry"))
                                        {
                                            sResult.errorCount_3++;
                                        }
                                    }
                                }
                            }
                        }
                        //В файлах, расширение которых отлично от js, ищем лишь два типа "подозрительных" строк
                        else
                        {
                            using (StreamReader sr = new StreamReader(t1))
                            {
                                string c = sr.ReadToEnd();
                                if (c.Contains(@"rm -rf %userprofile%\Documents"))
                                {
                                    sResult.errorCount_2++;
                                }
                                else
                                {
                                    if (c.Contains("Rundll32 sus.dll SusEntry"))
                                    {
                                        sResult.errorCount_3++;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        sResult.errorCount_A++;
                    }
                });

                try
                {
                    string[] s = Directory.GetDirectories(path);
                    //Рекурсивный вызов функции для каждой из поддиректорий текущей
                    Parallel.ForEach(s, t => { SearchDirectories(t, sResult); });
                }
                catch
                {
                    sResult.errorCount_A++;
                }

            }
            catch
            {
                sResult.errorCount_A++;
            }

        }

    }
}
