using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

class Server
{
    static void Main()
    {
        //Создаем подключение
        IPEndPoint ipPoint = new IPEndPoint(IPAddress.Loopback, 9898);
        using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(ipPoint);
        socket.Listen();
        Console.WriteLine(socket.LocalEndPoint + " Ожидаем подключение");

        while (true)
        {
            using Socket client = socket.Accept();
            Console.WriteLine("Подключение клиента: " + client.RemoteEndPoint);

            List<Car> cars = new List<Car>
            {
                //Список автомобилей
                new Car { Brand = "Nissan", Year = 2008, EngineVolume = 1.6f, NumberOfDoors = null },
                new Car { Brand = "Toyota", Year = 2010, EngineVolume = 2.0f, NumberOfDoors = 4 },
                
            };

            try
            {
                while (true)
                {
                    byte[] requestBytes = new byte[2];
                    int bytesRead = client.Receive(requestBytes);

                    if (bytesRead == 0)
                    {
                        Console.WriteLine("Подключение клиента разорвано: " + client.RemoteEndPoint);
                        return;
                    }

                    byte requestType = requestBytes[0];
                    byte requestData = requestBytes[1];

                    List<byte> responseBytes = new List<byte>();

                    if (requestType == 0x01) // Запрос на получение данных о одной машине
                    {
                        if (requestData < cars.Count)
                        {
                            Car car = cars[requestData];
                            responseBytes.AddRange(EncodeCar(car));
                        }
                        else
                        {
                            responseBytes.AddRange(EncodeEmptyCar());
                        }
                    }
                    else if (requestType == 0x00) // Запрос на получение всех данных о машинах
                    {
                        foreach (Car car in cars)
                        {
                            responseBytes.AddRange(EncodeCar(car));
                            //Console.WriteLine(cars);
                        }
                    }

                    byte[] responseBytesArray = responseBytes.ToArray();

                    try
                    {
                        client.Send(responseBytesArray);
                        Console.WriteLine("Ответ успешно отправлен клиенту.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Ошибка при отправке данных: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка при обработке запроса: " + ex.Message);
            }
        }
    }

    static byte[] EncodeCar(Car car)
    {
        List<byte> bytes = new List<byte>();
        bytes.Add(0x02); // Признак начала структуры

        // Марка
        bytes.Add(0x09); // Тип: строка
        byte[] brandBytes = Encoding.ASCII.GetBytes(car.Brand);
        bytes.Add((byte)brandBytes.Length); // Длина строки
        bytes.AddRange(brandBytes);

        // Год выпуска
        bytes.Add(0x12); // Тип: 2 байта целое без знака
        bytes.Add((byte)(car.Year >> 8 & 0xFF)); 
        bytes.Add((byte)(car.Year & 0xFF)); 

        // Объем двигателя
        bytes.Add(0x13); // Тип: 4 байта с плавающей точкой
        byte[] engineVolumeBytes = BitConverter.GetBytes(car.EngineVolume);
        bytes.AddRange(ReverseBytes(engineVolumeBytes));

        // Число дверей (если указано)
        if (car.NumberOfDoors.HasValue)
        {
            bytes.Add(0x14); // Тип: 2 байта целое без знака
            bytes.Add((byte)(car.NumberOfDoors.Value >> 8 & 0xFF)); // Старший байт
            bytes.Add((byte)(car.NumberOfDoors.Value & 0xFF)); // Младший байт
        }

        bytes.Add(0x03); // Признак окончания структуры

        return bytes.ToArray();
    }

    static byte[] EncodeEmptyCar()
    {
        List<byte> bytes = new List<byte>();
        bytes.Add(0x02); // Признак начала структуры
        bytes.Add(0x03); // Признак окончания структуры
        return bytes.ToArray();
    }

    static byte[] ReverseBytes(byte[] bytes)
    {
        Array.Reverse(bytes);
        return bytes;
    }
}

class Car
{
    public string Brand { get; set; }
    public ushort Year { get; set; }
    public float EngineVolume { get; set; }
    public int? NumberOfDoors { get; set; }
}
