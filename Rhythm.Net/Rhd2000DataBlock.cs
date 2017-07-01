using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Rhythm.Net
{
    /// <summary>
    /// This class creates a data structure storing data samples from a Rhythm FPGA interface
    /// controlling up to 8 RHD2000 chips. Typically, instances of <see cref="Rhd2000DataBlock"/>
    /// will be created dynamically as data becomes available over the USB interface and appended
    /// to a queue that will be used to stream the data to disk or to a GUI display.
    /// </summary>
    public class Rhd2000DataBlock
    {
        const int SAMPLES_PER_DATA_BLOCK_USB2 = 60;
        const int SAMPLES_PER_DATA_BLOCK_USB3 = 256;
        const ulong RHD2000_HEADER_MAGIC_NUMBER = 0xc691199927021942;

        uint[] timeStamp;
        int[][,] amplifierData;
        int[][,] auxiliaryData;
        int[,] boardAdcData;
        int[] ttlIn;
        int[] ttlOut;

        int samplesPerBlock;
        int dataBlockSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="Rhd2000DataBlock"/> class.
        /// Allocates memory for a data block supporting the specified number of data streams.
        /// </summary>
        /// <param name="numDataStreams">The number of available data streams.</param>
        /// <param name="usb3">Indicates whether the eval board supports USB3.</param>
        public Rhd2000DataBlock(int numDataStreams, bool usb3)
        {
            samplesPerBlock = GetSamplesPerDataBlock(usb3);
            dataBlockSize = CalculateDataBlockSizeInWords(numDataStreams, usb3);
            AllocateUIntArray1D(ref timeStamp, samplesPerBlock);
            AllocateIntArray3D(ref amplifierData, numDataStreams, 32, samplesPerBlock);
            AllocateIntArray3D(ref auxiliaryData, numDataStreams, 3, samplesPerBlock);
            AllocateIntArray2D(ref boardAdcData, 8, samplesPerBlock);
            AllocateIntArray1D(ref ttlIn, samplesPerBlock);
            AllocateIntArray1D(ref ttlOut, samplesPerBlock);
        }

        /// <summary>
        /// Gets the array of 32-bit sample timestamps.
        /// </summary>
        public uint[] Timestamp
        {
            get { return timeStamp; }
        }

        /// <summary>
        /// Gets the array of multidimensional amplifier data samples, indexed by data stream.
        /// </summary>
        public int[][,] AmplifierData
        {
            get { return amplifierData; }
        }

        /// <summary>
        /// Gets the array of multidimensional auxiliary data samples, indexed by data stream.
        /// </summary>
        public int[][,] AuxiliaryData
        {
            get { return auxiliaryData; }
        }

        /// <summary>
        /// Gets the multidimensional array of board ADC data samples.
        /// </summary>
        public int[,] BoardAdcData
        {
            get { return boardAdcData; }
        }

        /// <summary>
        /// Gets an array indicating the state of the 16 digital TTL input lines on the FPGA.
        /// </summary>
        public int[] TtlIn
        {
            get { return ttlIn; }
        }

        /// <summary>
        /// Gets an array indicating the state of the 16 digital TTL output lines on the FPGA.
        /// </summary>
        public int[] TtlOut
        {
            get { return ttlOut; }
        }

        /// <summary>
        /// Returns the number of 16-bit words in a USB data block with
        /// <paramref name="numDataStreams"/> data streams enabled.
        /// </summary>
        /// <param name="numDataStreams">The number of enabled data streams.</param>
        /// <param name="usb3">Indicates whether the eval board supports USB3.</param>
        /// <returns>The number of 16-bit words in the USB data block.</returns>
        public static int CalculateDataBlockSizeInWords(int numDataStreams, bool usb3)
        {
            return GetSamplesPerDataBlock(usb3) * (4 + 2 + numDataStreams * 36 + 8 + 2);
            // 4 = magic number; 2 = time stamp; 36 = (32 amp channels + 3 aux commands + 1 filler word); 8 = ADCs; 2 = TTL in/out
        }

        /// <summary>
        /// Returns the number of samples in a USB data block.
        /// </summary>
        /// <param name="usb3">Indicates whether the eval board supports USB3.</param>
        /// <returns>The number of samples in a USB data block.</returns>
        public static int GetSamplesPerDataBlock(bool usb3)
        {
            return usb3 ? SAMPLES_PER_DATA_BLOCK_USB3 : SAMPLES_PER_DATA_BLOCK_USB2;
        }

        /// <summary>
        /// Fills the data block with raw data from the nth data block in a USB input buffer
        /// in an <see cref="Rhd2000EvalBoard"/> object.
        /// </summary>
        /// <param name="usbBuffer">The raw USB input buffer data.</param>
        /// <param name="blockIndex">
        /// The index of the selected buffer data block. Setting blockIndex to 0 selects the
        /// first data block in the buffer, setting blockIndex to 1 selects the second data
        /// block, etc.
        /// </param>
        public void FillFromUsbBuffer(byte[] usbBuffer, int blockIndex)
        {
            int numDataStreams = amplifierData.Length;
            int index, t, channel, stream, i;

            index = blockIndex * 2 * dataBlockSize;
            for (t = 0; t < samplesPerBlock; ++t)
            {
                if (!CheckUsbHeader(usbBuffer, index))
                {
                    throw new ArgumentException("Incorrect header.", "usbBuffer");
                }
                index += 8;
                timeStamp[t] = ConvertUsbTimeStamp(usbBuffer, index);
                index += 4;

                // Read auxiliary results
                for (channel = 0; channel < 3; ++channel)
                {
                    for (stream = 0; stream < numDataStreams; ++stream)
                    {
                        auxiliaryData[stream][channel, t] = ConvertUsbWord(usbBuffer, index);
                        index += 2;
                    }
                }

                // Read amplifier channels
                for (channel = 0; channel < 32; ++channel)
                {
                    for (stream = 0; stream < numDataStreams; ++stream)
                    {
                        amplifierData[stream][channel, t] = ConvertUsbWord(usbBuffer, index);
                        index += 2;
                    }
                }

                // skip 36th filler word in each data stream
                index += 2 * numDataStreams;

                // Read from AD5662 ADCs
                for (i = 0; i < 8; ++i)
                {
                    boardAdcData[i, t] = ConvertUsbWord(usbBuffer, index);
                    index += 2;
                }

                // Read TTL input and output values
                ttlIn[t] = ConvertUsbWord(usbBuffer, index);
                index += 2;

                ttlOut[t] = ConvertUsbWord(usbBuffer, index);
                index += 2;
            }
        }

        // Print the contents of RHD2000 registers from a selected USB data stream (0-7)
        // to the console.
        /// <summary>
        /// Prints the contents of RHD2000 registers from a selected USB data stream (0-7)
        /// to the console. This function assumes that the command string generated by
        /// <see cref="Rhd2000Registers.CreateCommandListRegisterConfig"/> has been
        /// uploaded to the AuxCmd3 slot.
        /// </summary>
        /// <param name="stream">The index of the selected USB data stream.</param>
        public void Print(int stream)
        {
            const int RamOffset = 37;

            Console.WriteLine();
            Console.WriteLine("RHD 2000 Data Block contents:");
            Console.WriteLine("  ROM contents:");
            Console.WriteLine("    Chip Name: " +
                   (char)auxiliaryData[stream][2, 24] +
                   (char)auxiliaryData[stream][2, 25] +
                   (char)auxiliaryData[stream][2, 26] +
                   (char)auxiliaryData[stream][2, 27] +
                   (char)auxiliaryData[stream][2, 28] +
                   (char)auxiliaryData[stream][2, 29] +
                   (char)auxiliaryData[stream][2, 30] +
                   (char)auxiliaryData[stream][2, 31]);
            Console.WriteLine("    Company Name:" +
                   (char)auxiliaryData[stream][2, 32] +
                   (char)auxiliaryData[stream][2, 33] +
                   (char)auxiliaryData[stream][2, 34] +
                   (char)auxiliaryData[stream][2, 35] +
                   (char)auxiliaryData[stream][2, 36]);
            Console.WriteLine("    Intan Chip ID: " + auxiliaryData[stream][2, 19]);
            Console.WriteLine("    Number of Amps: " + auxiliaryData[stream][2, 20]);
            Console.Write("    Unipolar/Bipolar Amps: ");
            switch (auxiliaryData[stream][2, 21])
            {
                case 0:
                    Console.Write("bipolar");
                    break;
                case 1:
                    Console.Write("unipolar");
                    break;
                default:
                    Console.Write("UNKNOWN");
                    break;
            }
            Console.WriteLine();
            Console.WriteLine("    Die Revision: " + auxiliaryData[stream][2, 22]);
            Console.WriteLine("    Future Expansion Register: " + auxiliaryData[stream][2, 23]);

            Console.WriteLine("  RAM contents:");
            Console.WriteLine("    ADC reference BW:      " + ((auxiliaryData[stream][2, RamOffset + 0] & 0xc0) >> 6));
            Console.WriteLine("    amp fast settle:       " + ((auxiliaryData[stream][2, RamOffset + 0] & 0x20) >> 5));
            Console.WriteLine("    amp Vref enable:       " + ((auxiliaryData[stream][2, RamOffset + 0] & 0x10) >> 4));
            Console.WriteLine("    ADC comparator bias:   " + ((auxiliaryData[stream][2, RamOffset + 0] & 0x0c) >> 2));
            Console.WriteLine("    ADC comparator select: " + ((auxiliaryData[stream][2, RamOffset + 0] & 0x03) >> 0));
            Console.WriteLine("    VDD sense enable:      " + ((auxiliaryData[stream][2, RamOffset + 1] & 0x40) >> 6));
            Console.WriteLine("    ADC buffer bias:       " + ((auxiliaryData[stream][2, RamOffset + 1] & 0x3f) >> 0));
            Console.WriteLine("    MUX bias:              " + ((auxiliaryData[stream][2, RamOffset + 2] & 0x3f) >> 0));
            Console.WriteLine("    MUX load:              " + ((auxiliaryData[stream][2, RamOffset + 3] & 0xe0) >> 5));
            Console.WriteLine("    tempS2, tempS1:        " + ((auxiliaryData[stream][2, RamOffset + 3] & 0x10) >> 4) + "," +
                   ((auxiliaryData[stream][2, RamOffset + 3] & 0x08) >> 3));
            Console.WriteLine("    tempen:                " + ((auxiliaryData[stream][2, RamOffset + 3] & 0x04) >> 2));
            Console.WriteLine("    digout HiZ:            " + ((auxiliaryData[stream][2, RamOffset + 3] & 0x02) >> 1));
            Console.WriteLine("    digout:                " + ((auxiliaryData[stream][2, RamOffset + 3] & 0x01) >> 0));
            Console.WriteLine("    weak MISO:             " + ((auxiliaryData[stream][2, RamOffset + 4] & 0x80) >> 7));
            Console.WriteLine("    twoscomp:              " + ((auxiliaryData[stream][2, RamOffset + 4] & 0x40) >> 6));
            Console.WriteLine("    absmode:               " + ((auxiliaryData[stream][2, RamOffset + 4] & 0x20) >> 5));
            Console.WriteLine("    DSPen:                 " + ((auxiliaryData[stream][2, RamOffset + 4] & 0x10) >> 4));
            Console.WriteLine("    DSP cutoff freq:       " + ((auxiliaryData[stream][2, RamOffset + 4] & 0x0f) >> 0));
            Console.WriteLine("    Zcheck DAC power:      " + ((auxiliaryData[stream][2, RamOffset + 5] & 0x40) >> 6));
            Console.WriteLine("    Zcheck load:           " + ((auxiliaryData[stream][2, RamOffset + 5] & 0x20) >> 5));
            Console.WriteLine("    Zcheck scale:          " + ((auxiliaryData[stream][2, RamOffset + 5] & 0x18) >> 3));
            Console.WriteLine("    Zcheck conn all:       " + ((auxiliaryData[stream][2, RamOffset + 5] & 0x04) >> 2));
            Console.WriteLine("    Zcheck sel pol:        " + ((auxiliaryData[stream][2, RamOffset + 5] & 0x02) >> 1));
            Console.WriteLine("    Zcheck en:             " + ((auxiliaryData[stream][2, RamOffset + 5] & 0x01) >> 0));
            Console.WriteLine("    Zcheck DAC:            " + ((auxiliaryData[stream][2, RamOffset + 6] & 0xff) >> 0));
            Console.WriteLine("    Zcheck select:         " + ((auxiliaryData[stream][2, RamOffset + 7] & 0x3f) >> 0));
            Console.WriteLine("    ADC aux1 en:           " + ((auxiliaryData[stream][2, RamOffset + 9] & 0x80) >> 7));
            Console.WriteLine("    ADC aux2 en:           " + ((auxiliaryData[stream][2, RamOffset + 11] & 0x80) >> 7));
            Console.WriteLine("    ADC aux3 en:           " + ((auxiliaryData[stream][2, RamOffset + 13] & 0x80) >> 7));
            Console.WriteLine("    offchip RH1:           " + ((auxiliaryData[stream][2, RamOffset + 8] & 0x80) >> 7));
            Console.WriteLine("    offchip RH2:           " + ((auxiliaryData[stream][2, RamOffset + 10] & 0x80) >> 7));
            Console.WriteLine("    offchip RL:            " + ((auxiliaryData[stream][2, RamOffset + 12] & 0x80) >> 7));

            int rH1Dac1 = auxiliaryData[stream][2, RamOffset + 8] & 0x3f;
            int rH1Dac2 = auxiliaryData[stream][2, RamOffset + 9] & 0x1f;
            int rH2Dac1 = auxiliaryData[stream][2, RamOffset + 10] & 0x3f;
            int rH2Dac2 = auxiliaryData[stream][2, RamOffset + 11] & 0x1f;
            int rLDac1 = auxiliaryData[stream][2, RamOffset + 12] & 0x7f;
            int rLDac2 = auxiliaryData[stream][2, RamOffset + 13] & 0x3f;
            int rLDac3 = auxiliaryData[stream][2, RamOffset + 13] & 0x40 >> 6;

            double rH1 = 2630.0 + rH1Dac2 * 30800.0 + rH1Dac1 * 590.0;
            double rH2 = 8200.0 + rH2Dac2 * 38400.0 + rH2Dac1 * 730.0;
            double rL = 3300.0 + rLDac3 * 3000000.0 + rLDac2 * 15400.0 + rLDac1 * 190.0;

            Console.WriteLine("    RH1 DAC1, DAC2:        " + rH1Dac1.ToString("f2") + " " + rH1Dac2.ToString("f2") + " = " + (rH1 / 1000).ToString("f2") +
                    " kOhm");
            Console.WriteLine("    RH2 DAC1, DAC2:        " + rH2Dac1.ToString("f2") + " " + rH2Dac2.ToString("f2") + " = " + (rH2 / 1000).ToString("f2") +
                    " kOhm");
            Console.WriteLine("    RL DAC1, DAC2, DAC3:   " + rLDac1.ToString("f2") + " " + rLDac2.ToString("f2") + " " + rLDac3.ToString("f2") + " = " +
                    (rL / 1000).ToString("f2") + " kOhm");

            Console.WriteLine("    amp power[31:0]:       " +
                   ((auxiliaryData[stream][2, RamOffset + 17] & 0x80) >> 7).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 17] & 0x40) >> 6).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 17] & 0x20) >> 5).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 17] & 0x10) >> 4).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 17] & 0x08) >> 3).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 17] & 0x04) >> 2).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 17] & 0x02) >> 1).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 17] & 0x01) >> 0).ToString("f2") + " " +
                   ((auxiliaryData[stream][2, RamOffset + 16] & 0x80) >> 7).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 16] & 0x40) >> 6).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 16] & 0x20) >> 5).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 16] & 0x10) >> 4).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 16] & 0x08) >> 3).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 16] & 0x04) >> 2).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 16] & 0x02) >> 1).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 16] & 0x01) >> 0).ToString("f2") + " " +
                   ((auxiliaryData[stream][2, RamOffset + 15] & 0x80) >> 7).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 15] & 0x40) >> 6).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 15] & 0x20) >> 5).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 15] & 0x10) >> 4).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 15] & 0x08) >> 3).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 15] & 0x04) >> 2).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 15] & 0x02) >> 1).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 15] & 0x01) >> 0).ToString("f2") + " " +
                   ((auxiliaryData[stream][2, RamOffset + 14] & 0x80) >> 7).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 14] & 0x40) >> 6).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 14] & 0x20) >> 5).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 14] & 0x10) >> 4).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 14] & 0x08) >> 3).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 14] & 0x04) >> 2).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 14] & 0x02) >> 1).ToString("f2") +
                   ((auxiliaryData[stream][2, RamOffset + 14] & 0x01) >> 0).ToString("f2"));

            Console.WriteLine();

            int tempA = auxiliaryData[stream][1, 12];
            int tempB = auxiliaryData[stream][1, 20];
            int vddSample = auxiliaryData[stream][1, 28];

            double tempUnitsC = ((double)(tempB - tempA)) / 98.9 - 273.15;
            double tempUnitsF = (9.0 / 5.0) * tempUnitsC + 32.0;

            double vddSense = 0.0000748 * ((double)vddSample);

            Console.WriteLine("  Temperature sensor (only one reading): " + tempUnitsC.ToString("f1") + " C (" +
                    tempUnitsF.ToString("f1") + " F)");

            Console.WriteLine("  Supply voltage sensor                : " + vddSense.ToString("f2") + " V");
            Console.WriteLine();
        }

        // Write contents of data block to a binary output stream (saveOut) in little endian format.
        /// <summary>
        /// Writes the contents of a data block object to a binary output stream in little endian
        /// format (i.e., least significant byte first).
        /// </summary>
        /// <param name="saveOut">The binary output stream on which to write data block contents.</param>
        /// <param name="numDataStreams">The number of available data streams.</param>
        public void Write(Stream saveOut, int numDataStreams)
        {
            int t, channel, stream, i;

            for (t = 0; t < samplesPerBlock; ++t)
            {
                WriteWordLittleEndian(saveOut, (int)timeStamp[t]);
                for (channel = 0; channel < 32; ++channel)
                {
                    for (stream = 0; stream < numDataStreams; ++stream)
                    {
                        WriteWordLittleEndian(saveOut, amplifierData[stream][channel, t]);
                    }
                }
                for (channel = 0; channel < 3; ++channel)
                {
                    for (stream = 0; stream < numDataStreams; ++stream)
                    {
                        WriteWordLittleEndian(saveOut, auxiliaryData[stream][channel, t]);
                    }
                }
                for (i = 0; i < 8; ++i)
                {
                    WriteWordLittleEndian(saveOut, boardAdcData[i, t]);
                }
                WriteWordLittleEndian(saveOut, ttlIn[t]);
                WriteWordLittleEndian(saveOut, ttlOut[t]);
            }
        }

        // Allocates memory for a 3-D array of integers.
        void AllocateIntArray3D(ref int[][,] array3D, int xSize, int ySize, int zSize)
        {
            Array.Resize(ref array3D, xSize);
            for (int i = 0; i < xSize; ++i)
            {
                array3D[i] = new int[ySize, zSize];
            }
        }

        // Allocates memory for a 2-D array of integers.
        void AllocateIntArray2D(ref int[,] array2D, int xSize, int ySize)
        {
            array2D = new int[xSize, ySize];
        }

        // Allocates memory for a 1-D array of integers.
        void AllocateIntArray1D(ref int[] array1D, int xSize)
        {
            Array.Resize(ref array1D, xSize);
        }

        // Allocates memory for a 1-D array of unsigned integers.
        void AllocateUIntArray1D(ref uint[] array1D, int xSize)
        {
            Array.Resize(ref array1D, xSize);
        }

        // Write a 16-bit dataWord to an outputStream in "little endian" format (i.e., least significant
        // byte first).  We must do this explicitly for cross-platform consistency.  For example, Windows
        // is a little-endian OS, while Mac OS X and Linux can be little-endian or big-endian depending on
        // the processor running the operating system.
        //
        // (See "Endianness" article in Wikipedia for more information.)
        void WriteWordLittleEndian(Stream outputStream, int dataWord)
        {
            byte msb, lsb;

            lsb = (byte)((dataWord) & 0x00ff);
            msb = (byte)(((dataWord) & 0xff00) >> 8);

            outputStream.WriteByte(lsb);
            outputStream.WriteByte(msb);
        }

        // Check first 64 bits of USB header against the fixed Rhythm "magic number" to verify data sync.
        bool CheckUsbHeader(byte[] usbBuffer, int index)
        {
            ulong x1, x2, x3, x4, x5, x6, x7, x8;
            ulong header;

            x1 = usbBuffer[index];
            x2 = usbBuffer[index + 1];
            x3 = usbBuffer[index + 2];
            x4 = usbBuffer[index + 3];
            x5 = usbBuffer[index + 4];
            x6 = usbBuffer[index + 5];
            x7 = usbBuffer[index + 6];
            x8 = usbBuffer[index + 7];

            header = (x8 << 56) + (x7 << 48) + (x6 << 40) + (x5 << 32) + (x4 << 24) + (x3 << 16) + (x2 << 8) + (x1 << 0);
            return (header == RHD2000_HEADER_MAGIC_NUMBER);
        }

        // Read 32-bit time stamp from USB data frame.
        uint ConvertUsbTimeStamp(byte[] usbBuffer, int index)
        {
            uint x1, x2, x3, x4;
            x1 = usbBuffer[index];
            x2 = usbBuffer[index + 1];
            x3 = usbBuffer[index + 2];
            x4 = usbBuffer[index + 3];

            return (x4 << 24) + (x3 << 16) + (x2 << 8) + (x1 << 0);
        }

        // Convert two USB bytes into 16-bit word.
        int ConvertUsbWord(byte[] usbBuffer, int index)
        {
            uint x1, x2, result;

            x1 = (uint)usbBuffer[index];
            x2 = (uint)usbBuffer[index + 1];

            result = (x2 << 8) | (x1 << 0);

            return (int)result;
        }
    }
}
