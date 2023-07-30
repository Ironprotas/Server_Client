using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Serialization;

class Client
{

    static void Main()
    {
        IPEndPoint ipPoint = new IPEndPoint(IPAddress.Loopback, 9898);
        using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            //Подключение к серверу
            socket.Connect(ipPoint);
            Console.WriteLine("Успешное подключение к серверу");
            Console.WriteLine("Выберите режим получения данных:");
            Console.WriteLine("Нажмите 1 чтобы получить данные о одной машине");
            Console.WriteLine("Нажмите 0 чтобы получить все данные о машинах");
            string choice = Console.ReadLine();

            if (choice == "1")
            {
                Console.WriteLine("Введите индекс машины:");
                if (int.TryParse(Console.ReadLine(), out int carIndex))
                {
                    RequestSingleCar(socket, carIndex - 1);
                }
                else
                {
                    Console.WriteLine("Некорректный индекс машины");
                }
            }
            else if (choice == "0")
            {
                RequestAllCars(socket);
            }
            else
            {
                Console.WriteLine("Некорректный выбор");
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine("Ошибка при подключении к серверу: " + ex.SocketErrorCode);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка при подключении к серверу: " + ex.Message);
        }
    }

    static void RequestSingleCar(Socket socket, int carIndex)
    {
        byte[] requestBytes = new byte[] { 0x01, (byte)carIndex };

        try
        {
            socket.Send(requestBytes);

            byte[] responseBytes = new byte[65536];
            int bytesRead = socket.Receive(responseBytes);

            if (bytesRead > 0)
            {
                List<byte> parts = new List<byte>(responseBytes[..bytesRead]);
                PrintCarData(parts);
                SaveCarDataToXml(parts);
            }
            else
            {
                Console.WriteLine("Не удалось получить данные о машине");
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine("Ошибка при получении данных: " + ex.SocketErrorCode);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка при получении данных: " + ex.Message);
        }
    }

    static void RequestAllCars(Socket socket)
    {
        byte[] requestBytes = new byte[] { 0x00 };

        try
        {
            socket.Send(requestBytes);

            byte[] responseHeader = new byte[3];
            int headerBytesRead = socket.Receive(responseHeader);

            if (headerBytesRead == 3 && responseHeader[0] == 0x02 && responseHeader[1] == 0x03 && responseHeader[2] == 0x09)
            {
                List<Car> cars = new List<Car>();

                byte[] dataBytes = new byte[65536];
                int bytesRead = socket.Receive(dataBytes);

                if (bytesRead > 0)
                {
                    int offset = 0;
                    while (offset < bytesRead)
                    {
                        byte responseType = dataBytes[offset++];
                        if (responseType == 0x02)
                        {
                            byte brandLength = dataBytes[offset++];
                            string brand = Encoding.ASCII.GetString(dataBytes, offset, brandLength);
                            offset += brandLength;

                            ushort year = (ushort)((dataBytes[offset++] << 8) | dataBytes[offset++]);

                            byte[] engineVolumeBytes = new byte[4];
                            Array.Copy(dataBytes, offset, engineVolumeBytes, 0, 4);
                            if (BitConverter.IsLittleEndian)
                            {
                                Array.Reverse(engineVolumeBytes);
                            }
                            float engineVolume = BitConverter.ToSingle(engineVolumeBytes, 0);
                            offset += 4;

                            ushort doors = (ushort)((dataBytes[offset++] << 8) | dataBytes[offset++]);

                            Car car = new Car
                            {
                                Brand = brand,
                                Year = year,
                                EngineVolume = engineVolume,
                                NumberOfDoors = doors
                            };

                            cars.Add(car);
                        }
                    }

                    if (cars.Count > 0)
                    {
                        Console.WriteLine("Информация о всех машинах:");
                        foreach (Car car in cars)
                        {
                            Console.WriteLine("Марка: " + car.Brand);
                            Console.WriteLine("Год выпуска: " + car.Year);
                            Console.WriteLine("Объем двигателя: " + car.EngineVolume);
                            Console.WriteLine("Число дверей: " + car.NumberOfDoors);
                            Console.WriteLine();
                        }
                        Console.WriteLine("Данные сохранены в файл cars.xml");
                    }
                    else
                    {
                        Console.WriteLine("Не получено информации о машинах");
                    }
                }
                else
                {
                    Console.WriteLine("Ошибка при получении данных от сервера: нет данных");
                }
            }
            else
            {
                Console.WriteLine("Ошибка при получении данных от сервера");
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine("Ошибка при получении данных: " + ex.SocketErrorCode);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка при получении данных: " + ex.Message);
        }
    }

    static void PrintCarData(List<byte> parts)
    {
        int offset = 0;
        byte partType = parts[offset++];
        while (partType != 0x03) // Признак окончания структуры
        {
            if (partType == 0x09) // Марка
            {
                byte brandLength = parts[offset++];
                string brand = Encoding.ASCII.GetString(parts.GetRange(offset, brandLength).ToArray());
                Console.WriteLine("Марка: " + brand);
                offset += brandLength;
            }
            else if (partType == 0x12) // Год выпуска
            {
                ushort year = (ushort)((parts[offset] << 8) | parts[offset + 1]);
                Console.WriteLine("Год выпуска: " + year);
                offset += 2;
            }
            else if (partType == 0x13) // Объем двигателя
            {
                byte[] engineVolumeBytes = parts.GetRange(offset, 4).ToArray();
                float engineVolume = BitConverter.ToSingle(ReverseBytes(engineVolumeBytes), 0);
                Console.WriteLine("Объем двигателя: " + engineVolume);
                offset += 4;
            }
            else if (partType == 0x14) // Число дверей
            {
                ushort numberOfDoors = (ushort)((parts[offset] << 8) | parts[offset + 1]);
                Console.WriteLine("Число дверей: " + numberOfDoors);
                offset += 2;
            }

            partType = parts[offset++];
        }

        Console.WriteLine();
    }

    static void SaveCarDataToXml(List<byte> parts)
    {
        string brand = "";
        ushort year = 0;
        float engineVolume = 0;
        int? numberOfDoors = null;

        int offset = 0;
        byte partType = parts[offset++];
        while (partType != 0x03) 
        {
            if (partType == 0x09) // Марка
            {
                byte brandLength = parts[offset++];
                brand = Encoding.ASCII.GetString(parts.GetRange(offset, brandLength).ToArray());
                offset += brandLength;
            }
            else if (partType == 0x12) // Год выпуска
            {
                year = (ushort)((parts[offset] << 8) | parts[offset + 1]);
                offset += 2;
            }
            else if (partType == 0x13) // Объем двигателя
            {
                byte[] engineVolumeBytes = parts.GetRange(offset, 4).ToArray();
                engineVolume = BitConverter.ToSingle(ReverseBytes(engineVolumeBytes), 0);
                offset += 4;
            }
            else if (partType == 0x14) // Число дверей
            {
                numberOfDoors = (ushort)((parts[offset] << 8) | parts[offset + 1]);
                offset += 2;
            }

            partType = parts[offset++];
        }

        Car car = new Car
        {
            Brand = brand,
            Year = year,
            EngineVolume = engineVolume,
            NumberOfDoors = numberOfDoors
        };

        XmlSerializer serializer = new XmlSerializer(typeof(Car));
        using (StreamWriter writer = new StreamWriter("car.xml"))
        {
            serializer.Serialize(writer, car);
        }

        Console.WriteLine("Данные сохранены в файл 'car.xml'");
        Console.WriteLine("Нажмите любую клавишу для выхода...");
        Console.ReadKey();
    }

    static byte[] ReverseBytes(byte[] bytes)
    {
        //Функция которая переворачивает байты в обратном порядке
        Array.Reverse(bytes);
        return bytes;
    }
}
public class Car
{
    public string Brand { get; set; }
    public ushort? Year { get; set; }
    public float? EngineVolume { get; set; }
    public int? NumberOfDoors { get; set; }
}