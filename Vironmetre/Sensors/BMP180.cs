using System;
using System.Threading;
using Vironmetre.I2C;

namespace Vironmetre.Sensors
{
    public class BMP180
    {
        public const byte address = 0x77;

        #region private

        const byte REG_CONTROL = 0xF4;
        const byte REG_RESULT = 0xF6;
        const byte REG_AC1 = 0xAA;
        const byte REG_AC2 = 0xAC;
        const byte REG_AC3 = 0xAE;
        const byte REG_AC4 = 0xB0;
        const byte REG_AC5 = 0xB2;
        const byte REG_AC6 = 0xB4;
        const byte REG_B1 = 0xB6;
        const byte REG_B2 = 0xB8;
        const byte REG_MB = 0xBA;
        const byte REG_MC = 0xBC;
        const byte REG_MD = 0xBE;

        const byte COMMAND_TEMPERATURE = 0x2E;
        const byte COMMAND_PRESSURE0 = 0x34;
        const byte COMMAND_PRESSURE1 = 0x74;
        const byte COMMAND_PRESSURE2 = 0xB4;
        const byte COMMAND_PRESSURE3 = 0xF4;

        static string nl = Environment.NewLine;

        short AC1, AC2, AC3, VB1, VB2, MB, MC, MD;
        ushort AC4, AC5, AC6;

        double c5, c6, mc, md, x0, x1, x2, y0, y1, y2, p0, p1, p2;

        private I2CDeviceAPI api;

        private bool ReadInt16(byte register, ref short value)
        {
            byte[] bytes = new byte[2];

            // get the data
            if (api.WriteBytes(address,new byte[] { register }) && api.ReadBytes(address, bytes, 2))
            {
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(bytes);

                value = BitConverter.ToInt16(bytes, 0);
                return true;
            }
            return false;
        }

        private bool ReadUInt16(byte register, ref ushort value)
        {
            byte[] bytes = new byte[2];

            // get the data
            if (api.WriteBytes(address, new byte[] { register }) && api.ReadBytes(address, bytes, 2))
            {
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(bytes);

                value = BitConverter.ToUInt16(bytes, 0);
                return true;
            }

            return false;
        }

        #endregion

        public BMP180(I2CDeviceAPI api)
        {
            if (api == null)
                throw new ArgumentNullException();

            this.api = api;
        }

        /// <summary>
        /// Gets the calibration values which are unique to every BMP180 and are needed for the measurements calculation.
        /// </summary>
        /// <returns>True if operation was successful.</returns>
        public bool GetCalibrationValues()
        {
            double c3, c4, b1;

            if (ReadInt16(REG_AC1, ref AC1) &&
                ReadInt16(REG_AC2, ref AC2) &&
                ReadInt16(REG_AC3, ref AC3) &&
                ReadUInt16(REG_AC4, ref AC4) &&
                ReadUInt16(REG_AC5, ref AC5) &&
                ReadUInt16(REG_AC6, ref AC6) &&
                ReadInt16(REG_B1, ref VB1) &&
                ReadInt16(REG_B2, ref VB2) &&
                ReadInt16(REG_MB, ref MB) &&
                ReadInt16(REG_MC, ref MC) &&
                ReadInt16(REG_MD, ref MD))
            {
                // Compute floating-point polynominals

                
                Console.WriteLine($"AC1 = {AC1}" + nl + 
                                  $"AC2 = {AC2}" + nl +
                                  $"AC3 = {AC3}" + nl +
                                  $"AC4 = {AC4}" + nl +
                                  $"AC5 = {AC5}" + nl +
                                  $"AC6 = {AC6}" + nl +
                                  $"B1  = {VB1}" + nl +
                                  $"B2  = {VB2}" + nl +
                                  $"MB  = {MB}"  + nl +
                                  $"MC  = {MC}"  + nl +
                                  $"MD  = {MD}");
                

                c3 = 160.0 * Math.Pow(2, -15) * AC3;
                c4 = Math.Pow(10, -3) * Math.Pow(2, -15) * AC4;
                b1 = Math.Pow(160, 2) * Math.Pow(2, -30) * VB1;
                c5 = (Math.Pow(2, -15) / 160) * AC5;
                c6 = AC6;
                mc = (Math.Pow(2, 11) / Math.Pow(160, 2)) * MC;
                md = MD / 160.0;
                x0 = AC1;
                x1 = 160.0 * Math.Pow(2, -13) * AC2;
                x2 = Math.Pow(160, 2) * Math.Pow(2, -25) * VB2;
                y0 = c4 * Math.Pow(2, 15);
                y1 = c4 * c3;
                y2 = c4 * b1;
                p0 = (3791.0 - 8.0) / 1600.0;
                p1 = 1.0 - 7357.0 * Math.Pow(2, -20);
                p2 = 3038.0 * 100.0 * Math.Pow(2, -36);

                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the temperature in Celsius degrees.
        /// </summary>
        /// <param name="temperature">Value in which the temperature will be stored.</param>
        /// <returns>True if operation was successful.</returns>
        public bool GetTemperature(out double temperature)
        {
            byte[] data = new byte[2];
            double tu, a;
            data[0] = REG_CONTROL;
            data[1] = COMMAND_TEMPERATURE;
            temperature = 0;

            if (api.WriteBytes(address, data, 2))
            {
                Thread.Sleep(5);
                data[0] = REG_RESULT;

                if (api.WriteBytes(address, data, 1) && api.ReadBytes(address, data, 2))
                {
                    tu = (data[0] * 256.0) + data[1];
                    a = c5 * (tu - c6);
                    temperature = a + (mc / (a + md));

                    /*
                    Console.WriteLine($"data = {BitConverter.ToString(data)}");
                    Console.WriteLine($"tu = {tu}");
                    Console.WriteLine($"a  = {a}");
                    Console.WriteLine($"T  = {temperature}");
                    */
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Gets the pressure in milliBar.
        /// </summary>
        /// <param name="pressure">Value in which the pressure will be stored.</param>
        /// <param name="temperature">Previous temperature measurement which is necessary for calculation.</param>
        /// <param name="oversampling">Oversampling for measurement (value between 0 and 3).</param>
        /// <returns>True if operation was successful.</returns>
        public bool GetPressure(out double pressure, double temperature, byte oversampling)
        {
            if (oversampling > 3)
                throw new ArgumentOutOfRangeException(nameof(oversampling), "Oversampling value must be between 0 and 3");

            byte[] data = new byte[3] { REG_CONTROL, (byte)(0x34 + (oversampling << 6)), (byte)(2 + (3 << oversampling)) };
            double pu, s, x, y, z;
            pressure = 0;

            if (api.WriteBytes(address, data, 2))
            {
                Thread.Sleep(data[2]);

                data[0] = REG_RESULT;

                if (api.WriteBytes(address, data, 1) && api.ReadBytes(address, data, 3))
                {
                    //Console.WriteLine("data : " + BitConverter.ToString(data));

                    pu = (data[0] * 256.0) + data[1] + (data[2] / 256.0);
                    s = temperature - 25.0;
                    x = (x2 * Math.Pow(s, 2)) + (x1 * s) + x0;
                    y = (y2 * Math.Pow(s, 2)) + (y1 * s) + y0;
                    z = (pu - x) / y;
                    pressure = (p2 * Math.Pow(z, 2)) + (p1 * z) + p0;

                    /*
                    Console.WriteLine($"pu = {pu}");
                    Console.WriteLine($"s  = {s}");
                    Console.WriteLine($"x,y,z = {x},{y},{z}");
                    Console.WriteLine($"pressure = {pressure}");
                    */
                    return true;
                }
            }

            return false;
        }
    }
}
