using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vironmetre.I2C;
using Vironmetre.Sensors;

namespace Vironmetre
{
    class Program
    {
        static bool keepOnWorking = true;

        static void Main(string[] args)
        {
            using (TextWriter writer = new StreamWriter("log.txt"))
            {
                Console.SetError(writer);

                Console.CancelKeyPress += Console_CancelKeyPress;
                Console.WriteLine("Vironmetre simple \"I2C bridge\" demo");
                Console.WriteLine("with PIC18F25K20 and serial port");

                Test();

                Console.ReadKey();
            }
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            keepOnWorking = false;
        }

        static async void Test()
        {
            using (var port = ConfigureSerialPort())
            {
                if (port == null)
                {
                    Console.WriteLine("No port available");
                    return;
                }

                try
                {
                    I2CDeviceAPI api = new I2CDeviceAPI(port);

                    Console.WriteLine("Awaiting I2C device connection...");

                    string ls = port.ReadLine();

                    if (ls.Contains("LS"))
                    {
                        port.WriteLine("2000");
                        port.WriteLine("3000, 000E, V");
                        port.WriteLine("3000, 000F, C");
                        port.WriteLine("3001, 0011, V");
                        port.WriteLine("3002, 0013, V");
                        port.WriteLine("3002, 0014, C");
                        port.WriteLine("180F");
                        port.WriteLine("2A19, 0017, V");
                        port.WriteLine("2A19, 0018, C");
                        port.WriteLine("END");
                    }

                    byte address = 0;
                    bool devicePresent = api.I2CDevicePresent(ref address);

                    if (devicePresent)
                    {
                        switch (address)
                        {
                            case BMP180.address:
                                Console.WriteLine("BMP180!");

                                BMP180 barometer = new BMP180(api);

                                while (keepOnWorking)
                                {
                                    Console.WriteLine("Getting calibration values...");
                                    if (barometer.GetCalibrationValues())
                                    {
                                        Console.WriteLine("Got'em!");
                                        double temperature, pressure;

                                        while (keepOnWorking && barometer.GetTemperature(out temperature))
                                        {
                                            Console.WriteLine($"Temperature = {temperature.ToString("F3")}°C");

                                            if (keepOnWorking && barometer.GetPressure(out pressure, temperature, 0))
                                                Console.WriteLine($"Pressure    = {pressure.ToString("F3")} milliBar");

                                            Thread.Sleep(1000);
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Didn't work");
                                    }
                                }
                                break;
                            default:
                                Console.WriteLine("Unknown device");
                                break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.Message);
                }
            }
        }

        static SerialPort ConfigureSerialPort()
        {
            string portName;
            int baudrate = 9600;
            string line;
            string[] availablePorts = SerialPort.GetPortNames();

            if (availablePorts.Length == 0)
                return null;
            else
                portName = availablePorts[0];

            Console.WriteLine("Available serial ports : ");
            foreach (var item in availablePorts)
                Console.WriteLine($" - {item}");

            // get portname
            Console.WriteLine($"Type a portname or press return to use {portName}");
            do
            {
                line = Console.ReadLine();

                // if ()
                //     break;
            }
            while (!string.IsNullOrEmpty(line) && (!line.StartsWith("COM") || line.Length > 5 || !availablePorts.Contains(line)));

            if (!string.IsNullOrEmpty(line))
                portName = line;

            // get baudrate
            Console.WriteLine("Type a baudrate or press return to use 9600 baud/s");
            do
            {
                line = Console.ReadLine();

                if (string.IsNullOrEmpty(line))
                    break;

            } while (!int.TryParse(line, out baudrate));


            return new SerialPort(portName, baudrate);
        }
    }
}
