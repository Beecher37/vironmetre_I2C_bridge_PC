using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace Vironmetre.I2C
{
    public class I2CDeviceAPI
    {
        const byte CMD_READ  = 0x52; // 'R'
        const byte CMD_WRITE = 0x57; // 'W'

        const byte CMD_CLEAR_BUFFER = 0x00; // Send three times to clear
        const byte INFO_DEVICE_PLUGGED = 0x49; // 'I' 
        const byte INFO_DEVICE_ADDRESS = 0x41; // 'A'

        private SerialPort port;

        /// <summary>
        /// Creates a remote microcontroller I2C API from the serial port given as parameter.
        /// </summary>
        /// <param name="port">Serial port to use for the I2C API.</param>
        public I2CDeviceAPI(SerialPort port)
        {
            if (port == null)
                throw new ArgumentNullException();

            this.port = port;
            port.NewLine = "\r\n";
            port.Open();
            port.ReadTimeout = SerialPort.InfiniteTimeout;
            port.WriteTimeout = SerialPort.InfiniteTimeout;
        }

        /// <summary>
        /// Async task. Waits for the remote controller to send I2C device presence.
        /// </summary>
        /// <returns>True if there is an I2C device connected on the remote controller.</returns>
        public bool I2CDevicePresent(ref byte address)
        {
            address = 0;
            /*byte[] buffer = new byte[2];
            int length = buffer.Length;

            do
            {
                length -= await port.BaseStream.ReadAsync(buffer, 2-length, 1);
            } while (length > 0);

           // port.ReadTimeout = 200;
           // port.WriteTimeout = 200;

            return buffer[0] == INFO_DEVICE_PLUGGED && buffer[1] == 0x01;*/

            string line = port.ReadLine();

            Console.Error.WriteLine("R:" + line);

            if (line.StartsWith("SHW,000E,"))
            {
                line = line.Substring(9);
                if (Convert.ToByte(line.Substring(0, 2), 16) > 0)
                {
                    address = Convert.ToByte(line.Substring(2), 16);
                    return true;
                }
            }

            return false;
        }

        public bool ClearBuffer()
        {
            try
            {
                // Send info to microcontroller
                for (int i = 0; i < 3; i++)
                    port.BaseStream.WriteByte(CMD_CLEAR_BUFFER);

                Thread.Sleep(100);

                if (port.ReadExisting() == "OK")
                    return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
            return false;
        }

        /// <summary>
        /// Queries the remote controller for an I2C device presence.
        /// </summary>
        /// <returns>True if the device is present and there was no error.</returns>
        public bool TestI2CDevicePresence()
        {
            try
            {
                port.BaseStream.WriteByte(INFO_DEVICE_PLUGGED);
                Thread.Sleep(100);

                if (port.ReadByte() == INFO_DEVICE_PLUGGED)
                    return port.ReadByte() == 0x01;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
            return false;
        }

        /// <summary>
        /// Reads a byte array from a remote I2C device.
        /// </summary>
        /// <param name="deviceAddress">7-bit I2C device address.</param>
        /// <param name="readBuffer">Byte buffer to write.</param>
        /// <param name="length">(Optional) length of the byte buffer to read.</param>
        /// <returns>True if operation was successful.</returns>
        public bool ReadBytes(byte deviceAddress, byte[] readBuffer, byte length = 0)
        {
            if (length == 0)
                length = (byte)readBuffer.Length;

            try
            {
                // Send info to microcontroller

                string writenLine = $"WV,0011,01{length.ToString("X2")}.";
                port.WriteLine(writenLine);
                Console.Error.WriteLine("W:" + writenLine);

               // Thread.Sleep(10);

                string readString = port.ReadLine().Trim('\r', '\n');

                Console.Error.WriteLine("R:" + readString);

                if (readString.StartsWith("SHW,0013,"))
                {
                    readString = readString.Substring(9);

                    if (Convert.ToByte(readString.Substring(0, 2), 16) == 0x01 &&
                        Convert.ToByte(readString.Substring(2, 2), 16) == length)
                    {
                        readString = readString.Substring(4);
                        for (int i = 0; i < length; i++)
                        {
                            readBuffer[i] = Convert.ToByte(readString.Substring(0, 2), 16);
                            readString = readString.Substring(2);
                        }

                        return true;
                    }
                }

                return false;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
            return false;
        }

        /// <summary>
        /// Writes a byte array to a remote I2C device.
        /// </summary>
        /// <param name="deviceAddress">7-bit I2C device address.</param>
        /// <param name="writeBuffer">Byte buffer to write.</param>
        /// <param name="length">(Optional) length of the byte buffer to write.</param>
        /// <returns>True if operation was successful.</returns>
        public bool WriteBytes(byte deviceAddress, byte[] writeBuffer, byte length = 0)
        {
            if (length == 0)
                length = (byte)writeBuffer.Length;

            try
            {
                string wr = $"WV,0011,00{length.ToString("X2")}";

                for (int i = 0; i < length; i++)
                {
                    wr += writeBuffer[i].ToString("X2");
                }

                wr += ".";

                port.WriteLine(wr);
                Console.Error.WriteLine("W:" + wr);

//                Thread.Sleep(10);

                // Send info to microcontroller
                /*port.BaseStream.WriteByte(CMD_WRITE);
                port.BaseStream.WriteByte(deviceAddress);
                port.BaseStream.WriteByte(length);
                port.BaseStream.Write(writeBuffer, 0, length);
                */

                string line = port.ReadLine().Trim('\r','\n');

                Console.Error.WriteLine("R:" + line);
                if (line.StartsWith("SHW,0013,"))
                {
                    line = line.Substring(9);

                    if (Convert.ToByte(line.Substring(0, 2), 16) == 0x00 &&
                        Convert.ToByte(line.Substring(2, 2), 16) == length)
                    {
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
            return false;
        }
    }
}
