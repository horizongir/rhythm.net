using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using OpalKelly.FrontPanel;

namespace Rhythm.Net
{
    /// <summary>
    /// This class provides access to and control of the Opal Kelly XEM6010 USB/FPGA interface board running the Rhythm interface
    /// Verilog code. Only one instance of the <see cref="Rhd2000EvalBoard"/> object is needed to control a Rhythm-based FPGA interface.
    /// </summary>
    public class Rhd2000EvalBoard : IDisposable
    {
        bool disposed;
        const int USB_BUFFER_SIZE = 2560000;
        const uint RHYTHM_BOARD_ID_USB2 = 500;
        const uint RHYTHM_BOARD_ID_USB3 = 600;
        const int MAX_NUM_DATA_STREAMS_USB2 = 8;
        const int MAX_NUM_DATA_STREAMS_USB3 = 16;
        const int FIFO_CAPACITY_WORDS = 67108864;

        const int USB3_BLOCK_SIZE = 1024;
        const int DDR_BLOCK_SIZE = 32;

        okCFrontPanel dev;
        AmplifierSampleRate sampleRate;
        int numDataStreams; // total number of data streams currently enabled
        int[] dataStreamEnabled = new int[MAX_NUM_DATA_STREAMS_USB3]; // 0 (disabled) or 1 (enabled)
        int[] cableDelay = new int[4];

        // Buffer for reading bytes from USB interface
        byte[] usbBuffer = new byte[USB_BUFFER_SIZE];
        bool usb3;

        /// <summary>
        /// Initializes a new instance of the <see cref="Rhd2000EvalBoard"/> class.
        /// Sets the sampling rate to 30.0 kS/s/channel (FPGA default).
        /// </summary>
        public Rhd2000EvalBoard()
        {
            int i;
            sampleRate = AmplifierSampleRate.SampleRate30000Hz; // Rhythm FPGA boots up with 30.0 kS/s/channel sampling rate
            numDataStreams = 0;

            for (i = 0; i < MAX_NUM_DATA_STREAMS_USB3; ++i)
            {
                dataStreamEnabled[i] = 0;
            }
        }

        /// <summary>
        /// Finds an Opal Kelly XEM6010-LX45 board attached to a USB port and opens it.
        /// </summary>
        public void Open()
        {
            byte[] dll_date = new byte[32], dll_time = new byte[32];
            string serialNumber = "";
            int i, nDevices;

            Console.WriteLine("---- Intan Technologies ---- Rhythm RHD2000 Controller v1.0 ----\n");

            dev = new okCFrontPanel();
            Console.WriteLine("Scanning USB for Opal Kelly devices...\n");
            nDevices = dev.GetDeviceCount();
            Console.WriteLine("Found " + nDevices + " Opal Kelly device" + ((nDevices == 1) ? "" : "s") + " connected:");
            for (i = 0; i < nDevices; ++i)
            {
                Console.WriteLine("  Device #" + (i + 1) + ": Opal Kelly " + dev.GetDeviceListModel(i) + " with serial number " + dev.GetDeviceListSerial(i));
            }
            Console.WriteLine();

            // Find first device in list of type XEM6010LX45.
            for (i = 0; i < nDevices; ++i)
            {
                okCFrontPanel.BoardModel model = dev.GetDeviceListModel(i);
                if (model == okCFrontPanel.BoardModel.brdXEM6010LX45)
                {
                    serialNumber = dev.GetDeviceListSerial(i);
                    break;
                }
                else if (model == okCFrontPanel.BoardModel.brdXEM6310LX45)
                {
                    serialNumber = dev.GetDeviceListSerial(i);
                    usb3 = true;
                    break;
                }
            }

            // Attempt to open device.
            if (dev.OpenBySerial(serialNumber) != okCFrontPanel.ErrorCode.NoError)
            {
                usb3 = false;
                dev.Dispose();
                throw new InvalidOperationException("Device could not be opened.  Is one connected?");
            }

            // Configure the on-board PLL appropriately.
            dev.LoadDefaultPLLConfiguration();

            // Get some general information about the XEM.
            Console.WriteLine("FPGA system clock: " + GetSystemClockFreq() + " MHz"); // Should indicate 100 MHz
            Console.WriteLine("Opal Kelly device firmware version: " + dev.GetDeviceMajorVersion() + "." +
                    dev.GetDeviceMinorVersion());
            Console.WriteLine("Opal Kelly device serial number: " + dev.GetSerialNumber());
            Console.WriteLine("Opal Kelly device ID string: " + dev.GetDeviceID());
        }

        /// <summary>
        /// Uploads the Rhythm configuration file (i.e. bitfile) to the Xilinx FPGA on the Opal Kelly board.
        /// </summary>
        /// <param name="fileName">The path to the Rhythm configuration file.</param>
        public void UploadFpgaBitfile(string fileName)
        {
            okCFrontPanel.ErrorCode errorCode = dev.ConfigureFPGA(fileName);

            switch (errorCode)
            {
                case okCFrontPanel.ErrorCode.NoError: break;
                case okCFrontPanel.ErrorCode.DeviceNotOpen:
                    throw new InvalidOperationException("FPGA configuration failed: Device not open.");
                case okCFrontPanel.ErrorCode.FileError:
                    throw new InvalidOperationException("FPGA configuration failed: Cannot find configuration file.");
                case okCFrontPanel.ErrorCode.InvalidBitstream:
                    throw new InvalidOperationException("FPGA configuration failed: Bitstream is not properly formatted.");
                case okCFrontPanel.ErrorCode.DoneNotHigh:
                    throw new InvalidOperationException("FPGA configuration failed: FPGA DONE signal did not assert after configuration.");
                case okCFrontPanel.ErrorCode.TransferError:
                    throw new InvalidOperationException("FPGA configuration failed: USB error occurred during download.");
                case okCFrontPanel.ErrorCode.CommunicationError:
                    throw new InvalidOperationException("FPGA configuration failed: Communication error with firmware.");
                case okCFrontPanel.ErrorCode.UnsupportedFeature:
                    throw new InvalidOperationException("FPGA configuration failed: Unsupported feature.");
                default:
                    throw new InvalidOperationException("FPGA configuration failed: Unknown error.");
            }

            // Check for Opal Kelly FrontPanel support in the FPGA configuration.
            if (dev.IsFrontPanelEnabled() == false)
            {
                dev.Dispose();
                throw new InvalidOperationException("Opal Kelly FrontPanel support is not enabled in this FPGA configuration.");
            }

            uint boardId, boardVersion;
            dev.UpdateWireOuts();
            boardId = dev.GetWireOutValue(OkEndPoint.WireOutBoardId);
            boardVersion = dev.GetWireOutValue(OkEndPoint.WireOutBoardVersion);

            var rhythmBoardId = usb3 ? RHYTHM_BOARD_ID_USB3 : RHYTHM_BOARD_ID_USB2;
            if (boardId != rhythmBoardId)
            {
                throw new InvalidOperationException("FPGA configuration does not support Rhythm.  Incorrect board ID: " + boardId);
            }
            else
            {
                Console.WriteLine("Rhythm configuration file successfully loaded.  Rhythm version number: " + boardVersion + Environment.NewLine);
            }
        }

        /// <summary>
        /// Initializes Rhythm FPGA registers to default values.
        /// </summary>
        public void Initialize()
        {
            int i;

            ResetBoard();
            SetSampleRate(AmplifierSampleRate.SampleRate30000Hz);
            SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd1, 0);
            SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd1, 0);
            SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd1, 0);
            SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd1, 0);
            SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd2, 0);
            SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd2, 0);
            SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd2, 0);
            SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd2, 0);
            SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd3, 0);
            SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd3, 0);
            SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd3, 0);
            SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd3, 0);
            SelectAuxCommandLength(AuxCmdSlot.AuxCmd1, 0, 0);
            SelectAuxCommandLength(AuxCmdSlot.AuxCmd2, 0, 0);
            SelectAuxCommandLength(AuxCmdSlot.AuxCmd3, 0, 0);
            SetContinuousRunMode(true);
            SetMaxTimeStep(4294967295);  // 4294967295 == (2^32 - 1)

            SetCableLengthFeet(BoardPort.PortA, 3.0);  // assume 3 ft cables
            SetCableLengthFeet(BoardPort.PortB, 3.0);
            SetCableLengthFeet(BoardPort.PortC, 3.0);
            SetCableLengthFeet(BoardPort.PortD, 3.0);

            SetDspSettle(false);

            SetDataSource(0, BoardDataSource.PortA1);
            SetDataSource(1, BoardDataSource.PortB1);
            SetDataSource(2, BoardDataSource.PortC1);
            SetDataSource(3, BoardDataSource.PortD1);
            SetDataSource(4, BoardDataSource.PortA2);
            SetDataSource(5, BoardDataSource.PortB2);
            SetDataSource(6, BoardDataSource.PortC2);
            SetDataSource(7, BoardDataSource.PortD2);
            if (usb3)
            {
                SetDataSource(8, BoardDataSource.PortA1);
                SetDataSource(9, BoardDataSource.PortB1);
                SetDataSource(10, BoardDataSource.PortC1);
                SetDataSource(11, BoardDataSource.PortD1);
                SetDataSource(12, BoardDataSource.PortA2);
                SetDataSource(13, BoardDataSource.PortB2);
                SetDataSource(14, BoardDataSource.PortC2);
                SetDataSource(15, BoardDataSource.PortD2);
            }

            EnableDataStream(0, true);        // start with only one data stream enabled
            for (i = 1; i < MaxNumDataStreams(usb3); i++)
            {
                EnableDataStream(i, false);
            }

            ClearTtlOut();

            EnableDac(0, false);
            EnableDac(1, false);
            EnableDac(2, false);
            EnableDac(3, false);
            EnableDac(4, false);
            EnableDac(5, false);
            EnableDac(6, false);
            EnableDac(7, false);
            SelectDacDataStream(0, 0);
            SelectDacDataStream(1, 0);
            SelectDacDataStream(2, 0);
            SelectDacDataStream(3, 0);
            SelectDacDataStream(4, 0);
            SelectDacDataStream(5, 0);
            SelectDacDataStream(6, 0);
            SelectDacDataStream(7, 0);
            SelectDacDataChannel(0, 0);
            SelectDacDataChannel(1, 0);
            SelectDacDataChannel(2, 0);
            SelectDacDataChannel(3, 0);
            SelectDacDataChannel(4, 0);
            SelectDacDataChannel(5, 0);
            SelectDacDataChannel(6, 0);
            SelectDacDataChannel(7, 0);

            SetDacManual(32768);    // midrange value = 0 V

            SetDacGain(0);
            SetAudioNoiseSuppress(0);

            SetTtlMode(1);          // Digital outputs 0-7 are DAC comparators; 8-15 under manual control

            SetDacThreshold(0, 32768, true);
            SetDacThreshold(1, 32768, true);
            SetDacThreshold(2, 32768, true);
            SetDacThreshold(3, 32768, true);
            SetDacThreshold(4, 32768, true);
            SetDacThreshold(5, 32768, true);
            SetDacThreshold(6, 32768, true);
            SetDacThreshold(7, 32768, true);

            EnableExternalFastSettle(false);
            SetExternalFastSettleChannel(0);

            EnableExternalDigOut(BoardPort.PortA, false);
            EnableExternalDigOut(BoardPort.PortB, false);
            EnableExternalDigOut(BoardPort.PortC, false);
            EnableExternalDigOut(BoardPort.PortD, false);
            SetExternalDigOutChannel(BoardPort.PortA, 0);
            SetExternalDigOutChannel(BoardPort.PortB, 0);
            SetExternalDigOutChannel(BoardPort.PortC, 0);
            SetExternalDigOutChannel(BoardPort.PortD, 0);
        }

        /// <summary>
        /// Sets the per-channel sampling rate of the RHD2000 chips connected to the Rhythm FPGA.
        /// </summary>
        /// <param name="newSampleRate">The new per-channel sampling rate for RHD2000 chips connected to the Rhythm FPGA.</param>
        public void SetSampleRate(AmplifierSampleRate newSampleRate)
        {
            // Assuming a 100 MHz reference clock is provided to the FPGA, the programmable FPGA clock frequency
            // is given by:
            //
            //       FPGA internal clock frequency = 100 MHz * (M/D) / 2
            //
            // M and D are "multiply" and "divide" integers used in the FPGA's digital clock manager (DCM) phase-
            // locked loop (PLL) frequency synthesizer, and are subject to the following restrictions:
            //
            //                M must have a value in the range of 2 - 256
            //                D must have a value in the range of 1 - 256
            //                M/D must fall in the range of 0.05 - 3.33
            //
            // (See pages 85-86 of Xilinx document UG382 "Spartan-6 FPGA Clocking Resources" for more details.)
            //
            // This variable-frequency clock drives the state machine that controls all SPI communication
            // with the RHD2000 chips.  A complete SPI cycle (consisting of one CS pulse and 16 SCLK pulses)
            // takes 80 clock cycles.  The SCLK period is 4 clock cycles; the CS pulse is high for 14 clock
            // cycles between commands.
            //
            // Rhythm samples all 32 channels and then executes 3 "auxiliary" commands that can be used to read
            // and write from other registers on the chip, or to sample from the temperature sensor or auxiliary ADC
            // inputs, for example.  Therefore, a complete cycle that samples from each amplifier channel takes
            // 80 * (32 + 3) = 80 * 35 = 2800 clock cycles.
            //
            // So the per-channel sampling rate of each amplifier is 2800 times slower than the clock frequency.
            //
            // Based on these design choices, we can use the following values of M and D to generate the following
            // useful amplifier sampling rates for electrophsyiological applications:
            //
            //   M    D     clkout frequency    per-channel sample rate     per-channel sample period
            //  ---  ---    ----------------    -----------------------     -------------------------
            //    7  125          2.80 MHz               1.00 kS/s                 1000.0 usec = 1.0 msec
            //    7  100          3.50 MHz               1.25 kS/s                  800.0 usec
            //   21  250          4.20 MHz               1.50 kS/s                  666.7 usec
            //   14  125          5.60 MHz               2.00 kS/s                  500.0 usec
            //   35  250          7.00 MHz               2.50 kS/s                  400.0 usec
            //   21  125          8.40 MHz               3.00 kS/s                  333.3 usec
            //   14   75          9.33 MHz               3.33 kS/s                  300.0 usec
            //   28  125         11.20 MHz               4.00 kS/s                  250.0 usec
            //    7   25         14.00 MHz               5.00 kS/s                  200.0 usec
            //    7   20         17.50 MHz               6.25 kS/s                  160.0 usec
            //  112  250         22.40 MHz               8.00 kS/s                  125.0 usec
            //   14   25         28.00 MHz              10.00 kS/s                  100.0 usec
            //    7   10         35.00 MHz              12.50 kS/s                   80.0 usec
            //   21   25         42.00 MHz              15.00 kS/s                   66.7 usec
            //   28   25         56.00 MHz              20.00 kS/s                   50.0 usec
            //   35   25         70.00 MHz              25.00 kS/s                   40.0 usec
            //   42   25         84.00 MHz              30.00 kS/s                   33.3 usec
            //
            // To set a new clock frequency, assert new values for M and D (e.g., using okWireIn modules) and
            // pulse DCM_prog_trigger high (e.g., using an okTriggerIn module).  If this module is reset, it
            // reverts to a per-channel sampling rate of 30.0 kS/s.

            ulong M, D;

            switch (newSampleRate)
            {
                case AmplifierSampleRate.SampleRate1000Hz:
                    M = 7;
                    D = 125;
                    break;
                case AmplifierSampleRate.SampleRate1250Hz:
                    M = 7;
                    D = 100;
                    break;
                case AmplifierSampleRate.SampleRate1500Hz:
                    M = 21;
                    D = 250;
                    break;
                case AmplifierSampleRate.SampleRate2000Hz:
                    M = 14;
                    D = 125;
                    break;
                case AmplifierSampleRate.SampleRate2500Hz:
                    M = 35;
                    D = 250;
                    break;
                case AmplifierSampleRate.SampleRate3000Hz:
                    M = 21;
                    D = 125;
                    break;
                case AmplifierSampleRate.SampleRate3333Hz:
                    M = 14;
                    D = 75;
                    break;
                case AmplifierSampleRate.SampleRate4000Hz:
                    M = 28;
                    D = 125;
                    break;
                case AmplifierSampleRate.SampleRate5000Hz:
                    M = 7;
                    D = 25;
                    break;
                case AmplifierSampleRate.SampleRate6250Hz:
                    M = 7;
                    D = 20;
                    break;
                case AmplifierSampleRate.SampleRate8000Hz:
                    M = 112;
                    D = 250;
                    break;
                case AmplifierSampleRate.SampleRate10000Hz:
                    M = 14;
                    D = 25;
                    break;
                case AmplifierSampleRate.SampleRate12500Hz:
                    M = 7;
                    D = 10;
                    break;
                case AmplifierSampleRate.SampleRate15000Hz:
                    M = 21;
                    D = 25;
                    break;
                case AmplifierSampleRate.SampleRate20000Hz:
                    M = 28;
                    D = 25;
                    break;
                case AmplifierSampleRate.SampleRate25000Hz:
                    M = 35;
                    D = 25;
                    break;
                case AmplifierSampleRate.SampleRate30000Hz:
                    M = 42;
                    D = 25;
                    break;
                default:
                    throw new ArgumentException("Unsupported amplifier sampling rate.", "newSampleRate");
            }

            sampleRate = newSampleRate;

            // Wait for DcmProgDone = 1 before reprogramming clock synthesizer
            while (IsDcmProgDone() == false) { }

            // Reprogram clock synthesizer
            dev.SetWireInValue(OkEndPoint.WireInDataFreqPll, (uint)(256 * M + D));
            dev.UpdateWireIns();
            dev.ActivateTriggerIn(OkEndPoint.TrigInDcmProg, 0);

            // Wait for DataClkLocked = 1 before allowing data acquisition to continue
            while (IsDataClockLocked() == false) { }
        }

        /// <summary>
        /// Returns the current per-channel sampling rate (in Hz) as a floating-point number.
        /// </summary>
        /// <returns>The current per-channel sampling rate (in Hz) as a floating-point number.</returns>
        public double GetSampleRate()
        {
            switch (sampleRate)
            {
                case AmplifierSampleRate.SampleRate1000Hz:
                    return 1000.0;
                case AmplifierSampleRate.SampleRate1250Hz:
                    return 1250.0;
                case AmplifierSampleRate.SampleRate1500Hz:
                    return 1500.0;
                case AmplifierSampleRate.SampleRate2000Hz:
                    return 2000.0;
                case AmplifierSampleRate.SampleRate2500Hz:
                    return 2500.0;
                case AmplifierSampleRate.SampleRate3000Hz:
                    return 3000.0;
                case AmplifierSampleRate.SampleRate3333Hz:
                    return (10000.0 / 3.0);
                case AmplifierSampleRate.SampleRate4000Hz:
                    return 4000.0;
                case AmplifierSampleRate.SampleRate5000Hz:
                    return 5000.0;
                case AmplifierSampleRate.SampleRate6250Hz:
                    return 6250.0;
                case AmplifierSampleRate.SampleRate8000Hz:
                    return 8000.0;
                case AmplifierSampleRate.SampleRate10000Hz:
                    return 10000.0;
                case AmplifierSampleRate.SampleRate12500Hz:
                    return 12500.0;
                case AmplifierSampleRate.SampleRate15000Hz:
                    return 15000.0;
                case AmplifierSampleRate.SampleRate20000Hz:
                    return 20000.0;
                case AmplifierSampleRate.SampleRate25000Hz:
                    return 25000.0;
                case AmplifierSampleRate.SampleRate30000Hz:
                    return 30000.0;
                default:
                    return -1.0;
            }
        }

        /// <summary>
        /// Gets the current per-channel sampling rate as an <see cref="AmplifierSampleRate"/> enumeration.
        /// </summary>
        /// <returns>The current per-channel sampling rate as an <see cref="AmplifierSampleRate"/> enumeration.</returns>
        public AmplifierSampleRate GetSampleRateEnum()
        {
            return sampleRate;
        }

        /// <summary>
        /// Uploads a command list (generated by an instance of the <see cref="Rhd2000Registers"/> class) to a particular auxiliary command slot and
        /// RAM bank (0-15) on the FPGA.
        /// </summary>
        /// <param name="commandList">A command list generated by an instance of the <see cref="Rhd2000Registers"/> class.</param>
        /// <param name="auxCommandSlot">The auxiliary command slot on which to upload the command list.</param>
        /// <param name="bank">The RAM bank (0-15) on which to upload the command list.</param>
        public void UploadCommandList(List<int> commandList, AuxCmdSlot auxCommandSlot, uint bank)
        {
            if (auxCommandSlot != AuxCmdSlot.AuxCmd1 && auxCommandSlot != AuxCmdSlot.AuxCmd2 && auxCommandSlot != AuxCmdSlot.AuxCmd3)
            {
                throw new ArgumentException("auxCommandSlot out of range.", "auxCommandSlot");
            }

            if (bank < 0 || bank > 15)
            {
                throw new ArgumentException("bank out of range.", "bank");
            }

            for (uint i = 0; i < commandList.Count; ++i)
            {
                dev.SetWireInValue(OkEndPoint.WireInCmdRamData, (uint)commandList[(int)i]);
                dev.SetWireInValue(OkEndPoint.WireInCmdRamAddr, i);
                dev.SetWireInValue(OkEndPoint.WireInCmdRamBank, bank);
                dev.UpdateWireIns();
                switch (auxCommandSlot)
                {
                    case AuxCmdSlot.AuxCmd1:
                        dev.ActivateTriggerIn(OkEndPoint.TrigInRamWrite, 0);
                        break;
                    case AuxCmdSlot.AuxCmd2:
                        dev.ActivateTriggerIn(OkEndPoint.TrigInRamWrite, 1);
                        break;
                    case AuxCmdSlot.AuxCmd3:
                        dev.ActivateTriggerIn(OkEndPoint.TrigInRamWrite, 2);
                        break;
                }
            }
        }

        /// <summary>
        /// Prints a command list (generated by an instance of the <see cref="Rhd2000Registers"/> class) to the console in readable form, for
        /// diagnostic purposes.
        /// </summary>
        /// <param name="commandList">A command list generated by an instance of the <see cref="Rhd2000Registers"/> class.</param>
        public void PrintCommandList(List<int> commandList)
        {
            int cmd, channel, reg, data;

            Console.WriteLine();
            for (int i = 0; i < commandList.Count; ++i)
            {
                cmd = commandList[i];
                if (cmd < 0 || cmd > 0xffff)
                {
                    Console.WriteLine("  command[" + i + "] = INVALID COMMAND: " + cmd);
                }
                else if ((cmd & 0xc000) == 0x0000)
                {
                    channel = (cmd & 0x3f00) >> 8;
                    Console.WriteLine("  command[" + i + "] = CONVERT(" + channel + ")");
                }
                else if ((cmd & 0xc000) == 0xc000)
                {
                    reg = (cmd & 0x3f00) >> 8;
                    Console.WriteLine("  command[" + i + "] = READ(" + reg + ")");
                }
                else if ((cmd & 0xc000) == 0x8000)
                {
                    reg = (cmd & 0x3f00) >> 8;
                    data = (cmd & 0x00ff);
                    Console.WriteLine("  command[" + i + "] = WRITE(" + reg + "," + data.ToString("x") + ")");
                }
                else if (cmd == 0x5500)
                {
                    Console.WriteLine("  command[" + i + "] = CALIBRATE");
                }
                else if (cmd == 0x6a00)
                {
                    Console.WriteLine("  command[" + i + "] = CLEAR");
                }
                else
                {
                    Console.WriteLine("  command[" + i + "] = INVALID COMMAND: " + cmd.ToString("x"));
                }
            }
            Console.WriteLine();
        }

        /// <summary>
        /// Selects an auxiliary command slot (AuxCmd1, AuxCmd2, or AuxCmd3) and bank (0-15) for a particular SPI port.
        /// </summary>
        /// <param name="port">The SPI port on which the auxiliary command slot is selected.</param>
        /// <param name="auxCommandSlot">The auxiliary command slot to be selected.</param>
        /// <param name="bank">The RAM bank (0-15) to be selected.</param>
        public void SelectAuxCommandBank(BoardPort port, AuxCmdSlot auxCommandSlot, int bank)
        {
            int bitShift;

            if (auxCommandSlot != AuxCmdSlot.AuxCmd1 && auxCommandSlot != AuxCmdSlot.AuxCmd2 && auxCommandSlot != AuxCmdSlot.AuxCmd3)
            {
                throw new ArgumentException("auxCommandSlot out of range.", "auxCommandSlot");
            }

            if (bank < 0 || bank > 15)
            {
                throw new ArgumentException("bank out of range.", "bank");
            }

            switch (port)
            {
                case BoardPort.PortA:
                    bitShift = 0;
                    break;
                case BoardPort.PortB:
                    bitShift = 4;
                    break;
                case BoardPort.PortC:
                    bitShift = 8;
                    break;
                case BoardPort.PortD:
                    bitShift = 12;
                    break;
                default:
                    throw new ArgumentException("port out of range.", "port");
            }

            switch (auxCommandSlot)
            {
                case AuxCmdSlot.AuxCmd1:
                    dev.SetWireInValue(OkEndPoint.WireInAuxCmdBank1, (uint)(bank << bitShift), (uint)(0x000f << bitShift));
                    break;
                case AuxCmdSlot.AuxCmd2:
                    dev.SetWireInValue(OkEndPoint.WireInAuxCmdBank2, (uint)(bank << bitShift), (uint)(0x000f << bitShift));
                    break;
                case AuxCmdSlot.AuxCmd3:
                    dev.SetWireInValue(OkEndPoint.WireInAuxCmdBank3, (uint)(bank << bitShift), (uint)(0x000f << bitShift));
                    break;
            }
            dev.UpdateWireIns();
        }

        /// <summary>
        /// Specifies a command sequence end point (endIndex = 0-1023) and command loop index (loopIndex = 0-1023) for a particular
        /// auxiliary command slot (AuxCmd1, AuxCmd2, or AuxCmd3).
        /// </summary>
        /// <param name="auxCommandSlot">The auxiliary command slot on which to specify the command sequence length.</param>
        /// <param name="loopIndex">The command sequence loop index (0-1023).</param>
        /// <param name="endIndex">The command sequence end point index (0-1023).</param>
        public void SelectAuxCommandLength(AuxCmdSlot auxCommandSlot, int loopIndex, int endIndex)
        {
            if (auxCommandSlot != AuxCmdSlot.AuxCmd1 && auxCommandSlot != AuxCmdSlot.AuxCmd2 && auxCommandSlot != AuxCmdSlot.AuxCmd3)
            {
                throw new ArgumentException("auxCommandSlot out of range.", "auxCommandSlot");
            }

            if (loopIndex < 0 || loopIndex > 1023)
            {
                throw new ArgumentException("loopIndex out of range.", "loopIndex");
            }

            if (endIndex < 0 || endIndex > 1023)
            {
                throw new ArgumentException("endIndex out of range.", "endIndex");
            }

            switch (auxCommandSlot)
            {
                case AuxCmdSlot.AuxCmd1:
                    dev.SetWireInValue(OkEndPoint.WireInAuxCmdLoop1, (uint)loopIndex);
                    dev.SetWireInValue(OkEndPoint.WireInAuxCmdLength1, (uint)endIndex);
                    break;
                case AuxCmdSlot.AuxCmd2:
                    dev.SetWireInValue(OkEndPoint.WireInAuxCmdLoop2, (uint)loopIndex);
                    dev.SetWireInValue(OkEndPoint.WireInAuxCmdLength2, (uint)endIndex);
                    break;
                case AuxCmdSlot.AuxCmd3:
                    dev.SetWireInValue(OkEndPoint.WireInAuxCmdLoop3, (uint)loopIndex);
                    dev.SetWireInValue(OkEndPoint.WireInAuxCmdLength3, (uint)endIndex);
                    break;
            }
            dev.UpdateWireIns();
        }

        /// <summary>
        /// Resets the FPGA. This clears all auxiliary command RAM banks, clears the USB FIFO, and resets the
        /// per-channel sampling rate to its default value of 30.0 kS/s/channel.
        /// </summary>
        public void ResetBoard()
        {
            dev.SetWireInValue(OkEndPoint.WireInResetRun, 0x01, 0x01);
            dev.UpdateWireIns();
            dev.SetWireInValue(OkEndPoint.WireInResetRun, 0x00, 0x01);
            dev.UpdateWireIns();
            if (usb3)
            {
                dev.SetWireInValue(OkEndPoint.WireInMultiUse, USB3_BLOCK_SIZE / 4);
                dev.UpdateWireIns();
                dev.ActivateTriggerIn(OkEndPoint.TrigInOpenEphys, 16);
                Console.WriteLine("Blocksize set to " + USB3_BLOCK_SIZE);
                dev.SetWireInValue(OkEndPoint.WireInMultiUse, DDR_BLOCK_SIZE);
                dev.UpdateWireIns();
                dev.ActivateTriggerIn(OkEndPoint.TrigInOpenEphys, 17);
                Console.WriteLine("DDR burst set to " + DDR_BLOCK_SIZE);
            }
        }

        /// <summary>
        /// Sets the FPGA to run continuously once started (if continuousMode is set to true) or to run until
        /// maxTimeStep is reached (if continuousMode is set to false).
        /// </summary>
        /// <param name="continuousMode">
        /// Set the FPGA to run continuously once started if set to true or to run until
        /// maxTimeStep is reached if set to false.
        /// </param>
        public void SetContinuousRunMode(bool continuousMode)
        {
            if (continuousMode)
            {
                dev.SetWireInValue(OkEndPoint.WireInResetRun, 0x02, 0x02);
            }
            else
            {
                dev.SetWireInValue(OkEndPoint.WireInResetRun, 0x00, 0x02);
            }
            dev.UpdateWireIns();

        }

        /// <summary>
        /// Sets maxTimeStep for cases where continuousMode is set to false.
        /// </summary>
        /// <param name="maxTimeStep">
        /// The maxTimeStep (in number of samples) for which to run the
        /// interface when continuousMode is set to false.
        /// </param>
        public void SetMaxTimeStep(uint maxTimeStep)
        {
            uint maxTimeStepLsb, maxTimeStepMsb;

            maxTimeStepLsb = maxTimeStep & 0x0000ffff;
            maxTimeStepMsb = maxTimeStep & 0xffff0000;

            dev.SetWireInValue(OkEndPoint.WireInMaxTimeStepLsb, maxTimeStepLsb);
            dev.SetWireInValue(OkEndPoint.WireInMaxTimeStepMsb, maxTimeStepMsb >> 16);
            dev.UpdateWireIns();
        }

        /// <summary>
        /// Starts SPI data acquisition.
        /// </summary>
        public void Run()
        {
            dev.ActivateTriggerIn(OkEndPoint.TrigInSpiStart, 0);
        }

        /// <summary>
        /// Returns true if the FPGA is currently running SPI data acquisition.
        /// </summary>
        /// <returns>True if the FPGA is currently running SPI data acquisition, false otherwise.</returns>
        public bool IsRunning()
        {
            uint value;

            dev.UpdateWireOuts();
            value = dev.GetWireOutValue(OkEndPoint.WireOutSpiRunning);

            if ((value & 0x01) == 0)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// Returns the number of 16-bit words in the USB FIFO. The user should never attempt to read
        /// more data than the FIFO currently contains, as it is not protected against underflow.
        /// </summary>
        /// <returns>The number of 16-bit words in the USB FIFO.</returns>
        public uint NumWordsInFifo()
        {
            dev.UpdateWireOuts();
            return (dev.GetWireOutValue(OkEndPoint.WireOutNumWordsMsb) << 16) + dev.GetWireOutValue(OkEndPoint.WireOutNumWordsLsb);
        }

        /// <summary>
        /// Returns the number of 16-bit words in the USB SDRAM FIFO can hold (67,108,864). The FIFO can actually hold a few
        /// thousand words more than this due to the on-FPGA mini-FIFOs used to interface with the SDRAM,
        /// but this function provides a conservative estimate of maximum FIFO capacity.
        /// </summary>
        /// <returns>The number of 16-bit words in the USB SDRAM FIFO.</returns>
        public static uint FifoCapacityInWords()
        {
            return FIFO_CAPACITY_WORDS;
        }

        /// <summary>
        /// Returns the maximum number of data streams available in the eval board.
        /// </summary>
        /// <param name="usb3">Indicates whether the eval board supports USB3.</param>
        /// <returns>The maximum number of data streams available in the eval board.</returns>
        public static int MaxNumDataStreams(bool usb3)
        {
            return usb3 ? MAX_NUM_DATA_STREAMS_USB3 : MAX_NUM_DATA_STREAMS_USB2;
        }

        /// <summary>
        /// Sets the delay for sampling the MISO line on a particular SPI port (PortA - PortD), in integer clock
        /// steps, where each clock step is 1/2800 of a per-channel sampling period.
        /// </summary>
        /// <param name="port">The SPI port for which to set the MISO line sampling delay.</param>
        /// <param name="delay">The delay for sampling the MISO line, in integer clock steps.</param>
        /// <remarks>
        /// Cable delay must be updated after any changes are made to the sampling rate, since cable delay
        /// calculations are based on the clock period.
        /// </remarks>
        public void SetCableDelay(BoardPort port, int delay)
        {
            int bitShift;

            if (delay < 0 || delay > 15)
            {
                Console.Error.WriteLine("Warning in Rhd2000EvalBoard.SetCableDelay: delay out of range.");
            }

            if (delay < 0) delay = 0;
            if (delay > 15) delay = 15;

            switch (port)
            {
                case BoardPort.PortA:
                    bitShift = 0;
                    cableDelay[0] = delay;
                    break;
                case BoardPort.PortB:
                    bitShift = 4;
                    cableDelay[1] = delay;
                    break;
                case BoardPort.PortC:
                    bitShift = 8;
                    cableDelay[2] = delay;
                    break;
                case BoardPort.PortD:
                    bitShift = 12;
                    cableDelay[3] = delay;
                    break;
                default:
                    throw new ArgumentException("port out of range.", "port");
            }

            dev.SetWireInValue(OkEndPoint.WireInMisoDelay, (uint)(delay << bitShift), (uint)(0x000f << bitShift));
            dev.UpdateWireIns();
        }

        /// <summary>
        /// Sets the delay for sampling the MISO line on a particular SPI port (PortA - PortD) based on the length
        /// of the cable between the FPGA and the RHD2000 chip (in meters).
        /// </summary>
        /// <param name="port">The SPI port for which to set the MISO line sampling delay.</param>
        /// <param name="lengthInMeters">The length of the cable between the FPGA and the RHD2000 chip (in meters).</param>
        /// <remarks>
        /// Cable delay must be updated after any changes are made to the sampling rate, since cable delay
        /// calculations are based on the clock period.
        /// </remarks>
        public void SetCableLengthMeters(BoardPort port, double lengthInMeters)
        {
            int delay;
            double tStep, cableVelocity, distance, timeDelay;
            const double speedOfLight = 299792458.0;  // units = meters per second
            const double xilinxLvdsOutputDelay = 1.9e-9;    // 1.9 ns Xilinx LVDS output pin delay
            const double xilinxLvdsInputDelay = 1.4e-9;     // 1.4 ns Xilinx LVDS input pin delay
            const double rhd2000Delay = 9.0e-9;             // 9.0 ns RHD2000 SCLK-to-MISO delay
            const double misoSettleTime = 6.7e-9;          // 6.7 ns delay after MISO changes, before we sample it

            tStep = 1.0 / (2800.0 * GetSampleRate());  // data clock that samples MISO has a rate 35 x 80 = 2800x higher than the sampling rate
            cableVelocity = 0.555 * speedOfLight;  // propogation velocity on cable: improvement based on cable measurements
            distance = 2.0 * lengthInMeters;      // round trip distance data must travel on cable
            timeDelay = (distance / cableVelocity) + xilinxLvdsOutputDelay + rhd2000Delay + xilinxLvdsInputDelay + misoSettleTime;

            delay = (int)Math.Floor(((timeDelay / tStep) + 1.0) + 0.5);

            if (delay < 1) delay = 1;   // delay of zero is too short (due to I/O delays), even for zero-length cables

            SetCableDelay(port, delay);
        }

        /// <summary>
        /// Sets the delay for sampling the MISO line on a particular SPI port (PortA - PortD) based on the length
        /// of the cable between the FPGA and the RHD2000 chip (in feet).
        /// </summary>
        /// <param name="port">The SPI port for which to set the MISO line sampling delay.</param>
        /// <param name="lengthInFeet">The length of the cable between the FPGA and the RHD2000 chip (in feet).</param>
        /// <remarks>
        /// Cable delay must be updated after any changes are made to the sampling rate, since cable delay
        /// calculations are based on the clock period.
        /// </remarks>
        public void SetCableLengthFeet(BoardPort port, double lengthInFeet)
        {
            SetCableLengthMeters(port, 0.3048 * lengthInFeet);   // convert feet to meters
        }

        /// <summary>
        /// Estimates the cable length (in meters) between the FPGA and the RHD2000 chip based on a particular delay
        /// used in setCableDelay and the current sampling rate.
        /// </summary>
        /// <param name="delay">The delay for sampling the MISO line, in integer clock steps.</param>
        /// <returns>The estimated cable length (in meters) between the FPGA and the RHD2000 chip.</returns>
        public double EstimateCableLengthMeters(int delay)
        {
            double tStep, cableVelocity, distance;
            const double speedOfLight = 299792458.0;  // units = meters per second
            const double xilinxLvdsOutputDelay = 1.9e-9;    // 1.9 ns Xilinx LVDS output pin delay
            const double xilinxLvdsInputDelay = 1.4e-9;     // 1.4 ns Xilinx LVDS input pin delay
            const double rhd2000Delay = 9.0e-9;             // 9.0 ns RHD2000 SCLK-to-MISO delay
            const double misoSettleTime = 6.7e-9;          // 6.7 ns delay after MISO changes, before we sample it

            tStep = 1.0 / (2800.0 * GetSampleRate());  // data clock that samples MISO has a rate 35 x 80 = 2800x higher than the sampling rate
            cableVelocity = 0.555 * speedOfLight;  // propogation velocity on cable: improvement based on cable measurements

            distance = cableVelocity * ((((double)delay) - 1.0) * tStep - (xilinxLvdsOutputDelay + rhd2000Delay + xilinxLvdsInputDelay + misoSettleTime));
            if (distance < 0.0) distance = 0.0;

            return (distance / 2.0);
        }

        /// <summary>
        /// Estimates the cable length (in feet) between the FPGA and the RHD2000 chip based on a particular delay
        /// used in setCableDelay and the current sampling rate.
        /// </summary>
        /// <param name="delay">The delay for sampling the MISO line, in integer clock steps.</param>
        /// <returns>The estimated cable length (in feet) between the FPGA and the RHD2000 chip.</returns>
        public double EstimateCableLengthFeet(int delay)
        {
            return 3.2808 * EstimateCableLengthMeters(delay);
        }

        /// <summary>
        /// Turns on or off the DSP settle function in the FPGA. This only executes when CONVERT commands are executed
        /// by the RHD2000.
        /// </summary>
        /// <param name="enabled">Turns on DSP settle if set to true, turns it off otherwise.</param>
        public void SetDspSettle(bool enabled)
        {
            dev.SetWireInValue(OkEndPoint.WireInResetRun, (uint)(enabled ? 0x04 : 0x00), 0x04);
            dev.UpdateWireIns();
        }

        /// <summary>
        /// Assigns a particular data source (e.g., PortA1, PortA2, PortB1,...) to one of the eight
        /// available USB data streams (0-7).
        /// </summary>
        /// <param name="stream">The USB data stream (0-7) for which to assign the data source.</param>
        /// <param name="dataSource">
        /// The particular data source (e.g., PortA1, PortA2, PortB1,...) to assign
        /// to one of the available USB data streams.
        /// </param>
        public void SetDataSource(int stream, BoardDataSource dataSource)
        {
            int bitShift;
            int endPoint;

            switch (stream)
            {
                case 0:
                    endPoint = OkEndPoint.WireInDataStreamSel1234;
                    bitShift = 0;
                    break;
                case 1:
                    endPoint = OkEndPoint.WireInDataStreamSel1234;
                    bitShift = 4;
                    break;
                case 2:
                    endPoint = OkEndPoint.WireInDataStreamSel1234;
                    bitShift = 8;
                    break;
                case 3:
                    endPoint = OkEndPoint.WireInDataStreamSel1234;
                    bitShift = 12;
                    break;
                case 4:
                    endPoint = OkEndPoint.WireInDataStreamSel5678;
                    bitShift = 0;
                    break;
                case 5:
                    endPoint = OkEndPoint.WireInDataStreamSel5678;
                    bitShift = 4;
                    break;
                case 6:
                    endPoint = OkEndPoint.WireInDataStreamSel5678;
                    bitShift = 8;
                    break;
                case 7:
                    endPoint = OkEndPoint.WireInDataStreamSel5678;
                    bitShift = 12;
                    break;
                case 8:
                    endPoint = OkEndPoint.WireInDataStreamSel1234;
                    bitShift = 16;
                    break;
                case 9:
                    endPoint = OkEndPoint.WireInDataStreamSel1234;
                    bitShift = 20;
                    break;
                case 10:
                    endPoint = OkEndPoint.WireInDataStreamSel1234;
                    bitShift = 24;
                    break;
                case 11:
                    endPoint = OkEndPoint.WireInDataStreamSel1234;
                    bitShift = 28;
                    break;
                case 12:
                    endPoint = OkEndPoint.WireInDataStreamSel5678;
                    bitShift = 16;
                    break;
                case 13:
                    endPoint = OkEndPoint.WireInDataStreamSel5678;
                    bitShift = 20;
                    break;
                case 14:
                    endPoint = OkEndPoint.WireInDataStreamSel5678;
                    bitShift = 24;
                    break;
                case 15:
                    endPoint = OkEndPoint.WireInDataStreamSel5678;
                    bitShift = 28;
                    break;
                default:
                    throw new ArgumentException("stream out of range.", "stream");
            }

            dev.SetWireInValue(endPoint, (uint)((int)dataSource << bitShift), (uint)(0x000f << bitShift));
            dev.UpdateWireIns();
        }

        /// <summary>
        /// Enables or disables one of the eight available USB data streams (0-7).
        /// </summary>
        /// <param name="stream">The USB data stream (0-7) to enable or disable.</param>
        /// <param name="enabled">Enables the USB data stream if set to true or disables it if set to false.</param>
        public void EnableDataStream(int stream, bool enabled)
        {
            if (stream < 0 || stream > (MaxNumDataStreams(usb3) - 1))
            {
                throw new ArgumentException("stream out of range.", "stream");
            }

            if (enabled)
            {
                if (dataStreamEnabled[stream] == 0)
                {
                    dev.SetWireInValue(OkEndPoint.WireInDataStreamEn, (uint)(0x0001 << stream), (uint)(0x0001 << stream));
                    dev.UpdateWireIns();
                    dataStreamEnabled[stream] = 1;
                    ++numDataStreams;
                }
            }
            else
            {
                if (dataStreamEnabled[stream] == 1)
                {
                    dev.SetWireInValue(OkEndPoint.WireInDataStreamEn, (uint)(0x0000 << stream), (uint)(0x0001 << stream));
                    dev.UpdateWireIns();
                    dataStreamEnabled[stream] = 0;
                    numDataStreams--;
                }
            }
        }

        /// <summary>
        /// Returns the number of enabled USB data streams.
        /// </summary>
        /// <returns>The number of enabled USB data streams.</returns>
        public int GetNumEnabledDataStreams()
        {
            return numDataStreams;
        }

        /// <summary>
        /// Sets all 16 bits of the digital TTL output lines on the FPGA to zero.
        /// </summary>
        public void ClearTtlOut()
        {
            dev.SetWireInValue(OkEndPoint.WireInTtlOut, 0x0000);
            dev.UpdateWireIns();
        }

        /// <summary>
        /// Sets the 16 bits of the digital TTL output lines on the FPGA high or low according to an integer array.
        /// </summary>
        /// <param name="ttlOutArray">
        /// A length-16 array containing values of 0 or 1 to specify high or low bits in the TTL output lines.
        /// </param>
        public void SetTtlOut(int[] ttlOutArray)
        {
            int i, ttlOut;

            ttlOut = 0;
            for (i = 0; i < 16; ++i)
            {
                if (ttlOutArray[i] > 0)
                    ttlOut += 1 << i;
            }
            dev.SetWireInValue(OkEndPoint.WireInTtlOut, (uint)ttlOut);
            dev.UpdateWireIns();
        }

        /// <summary>
        /// Reads the 16 bits of the digital TTL input lines on the FPGA into an integer array.
        /// </summary>
        /// <param name="ttlInArray">
        /// A length-16 integer array that will contain the bits from the TTL input lines.
        /// </param>
        public void GetTtlIn(int[] ttlInArray)
        {
            int i, ttlIn;

            dev.UpdateWireOuts();
            ttlIn = (int)dev.GetWireOutValue(OkEndPoint.WireOutTtlIn);

            for (i = 0; i < 16; ++i)
            {
                ttlInArray[i] = 0;
                if ((ttlIn & (1 << i)) > 0)
                    ttlInArray[i] = 1;
            }
        }

        /// <summary>
        /// Sets the manual AD5662 DAC control WireIns to the specified value (0-65536).
        /// </summary>
        /// <param name="value">
        /// The 16-bit value (0-65536) to which the manual DAC control WireIns will be set.
        /// </param>
        public void SetDacManual(int value)
        {
            if (value < 0 || value > 65535)
            {
                throw new ArgumentException("value out of range.", "value");
            }

            dev.SetWireInValue(OkEndPoint.WireInDacManual, (uint)value);
            dev.UpdateWireIns();
        }

        /// <summary>
        /// Sets the eight red LEDs on the Opal Kelly XEM6010 board according to an integer array.
        /// </summary>
        /// <param name="ledArray">The length-8 integer array specifying the state of each of the eight red LEDs (0 or 1).</param>
        public void SetLedDisplay(int[] ledArray)
        {
            int i, ledOut;

            ledOut = 0;
            for (i = 0; i < 8; ++i)
            {
                if (ledArray[i] > 0)
                    ledOut += 1 << i;
            }
            dev.SetWireInValue(OkEndPoint.WireInLedDisplay, (uint)ledOut);
            dev.UpdateWireIns();
        }

        /// <summary>
        /// Enables or disables the AD5662 DACs connected to the FPGA.
        /// </summary>
        /// <param name="dacChannel">The AD5662 DAC channel (0-7) to enable or disable.</param>
        /// <param name="enabled">Enables the channel if set to true or disables it if set to false.</param>
        public void EnableDac(int dacChannel, bool enabled)
        {
            if (dacChannel < 0 || dacChannel > 7)
            {
                throw new ArgumentException("dacChannel out of range.", "dacChannel");
            }

            uint dacEnMask = (uint)(usb3 ? 0x0400 : 0x0200);
            switch (dacChannel)
            {
                case 0:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource1, (uint)(enabled ? dacEnMask : 0x0000), dacEnMask);
                    break;
                case 1:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource2, (uint)(enabled ? dacEnMask : 0x0000), dacEnMask);
                    break;
                case 2:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource3, (uint)(enabled ? dacEnMask : 0x0000), dacEnMask);
                    break;
                case 3:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource4, (uint)(enabled ? dacEnMask : 0x0000), dacEnMask);
                    break;
                case 4:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource5, (uint)(enabled ? dacEnMask : 0x0000), dacEnMask);
                    break;
                case 5:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource6, (uint)(enabled ? dacEnMask : 0x0000), dacEnMask);
                    break;
                case 6:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource7, (uint)(enabled ? dacEnMask : 0x0000), dacEnMask);
                    break;
                case 7:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource8, (uint)(enabled ? dacEnMask : 0x0000), dacEnMask);
                    break;
            }
            dev.UpdateWireIns();
        }

        /// <summary>
        /// Scales the digital signals to all eight AD5662 DACs by a factor of 2^<paramref name="gain"/>.
        /// </summary>
        /// <param name="gain">A number between 0 and 7 indicating the power of two by which to scale digital signals.</param>
        public void SetDacGain(int gain)
        {
            if (gain < 0 || gain > 7)
            {
                throw new ArgumentException("gain out of range.", "gain");
            }

            dev.SetWireInValue(OkEndPoint.WireInResetRun, (uint)(gain << 13), 0xe000);
            dev.UpdateWireIns();
        }

        /// <summary>
        /// Sets the noise slicing region for DAC channels 1 and 2 (i.e., audio left and right) to +/-16*<paramref name="noiseSuppress"/> LSBs,
        /// where noiseSuppress is between 0 and 127. This improves the audibility of weak neural spikes in noisy waveforms.
        /// </summary>
        /// <param name="noiseSuppress">A number between 0 and 127 specifying the audio noise suppression factor.</param>
        public void SetAudioNoiseSuppress(int noiseSuppress)
        {
            if (noiseSuppress < 0 || noiseSuppress > 127)
            {
                throw new ArgumentException("noiseSuppress out of range.", "noiseSuppress");
            }

            dev.SetWireInValue(OkEndPoint.WireInResetRun, (uint)(noiseSuppress << 6), 0x1fc0);
            dev.UpdateWireIns();
        }

        /// <summary>
        /// Assigns a particular data stream (0-7) to an AD5662 DAC channel (0-7). Setting stream
        /// to 8 selects DacManual1 value; setting stream to 9 selects DacManual2 value.
        /// </summary>
        /// <param name="dacChannel">The DAC channel to which the data stream will be assigned.</param>
        /// <param name="stream">The data stream to assign to the DAC channel.</param>
        public void SelectDacDataStream(int dacChannel, int stream)
        {
            if (dacChannel < 0 || dacChannel > 7)
            {
                throw new ArgumentException("dacChannel out of range.", "dacChannel");
            }

            if (stream < 0 || stream > MaxNumDataStreams(usb3) + 1)
            {
                throw new ArgumentException("stream out of range.", "stream");
            }

            uint dacStreamMask = (uint)(usb3 ? 0x03e0 : 0x01e0);
            switch (dacChannel)
            {
                case 0:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource1, (uint)(stream << 5), dacStreamMask);
                    break;
                case 1:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource2, (uint)(stream << 5), dacStreamMask);
                    break;
                case 2:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource3, (uint)(stream << 5), dacStreamMask);
                    break;
                case 3:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource4, (uint)(stream << 5), dacStreamMask);
                    break;
                case 4:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource5, (uint)(stream << 5), dacStreamMask);
                    break;
                case 5:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource6, (uint)(stream << 5), dacStreamMask);
                    break;
                case 6:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource7, (uint)(stream << 5), dacStreamMask);
                    break;
                case 7:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource8, (uint)(stream << 5), dacStreamMask);
                    break;
            }
            dev.UpdateWireIns();
        }

        /// <summary>
        /// Assigns a particular amplifier channel (0-31) to an AD5662 DAC channel (0-7).
        /// </summary>
        /// <param name="dacChannel">The DAC channel to which the amplifier channel will be assigned.</param>
        /// <param name="dataChannel">The amplifier channel to assign to the DAC channel.</param>
        public void SelectDacDataChannel(int dacChannel, int dataChannel)
        {
            if (dacChannel < 0 || dacChannel > 7)
            {
                throw new ArgumentException("dacChannel out of range.", "dacChannel");
            }

            if (dataChannel < 0 || dataChannel > 31)
            {
                throw new ArgumentException("dataChannel out of range.", "dataChannel");
            }

            switch (dacChannel)
            {
                case 0:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource1, (uint)(dataChannel << 0), 0x001f);
                    break;
                case 1:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource2, (uint)(dataChannel << 0), 0x001f);
                    break;
                case 2:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource3, (uint)(dataChannel << 0), 0x001f);
                    break;
                case 3:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource4, (uint)(dataChannel << 0), 0x001f);
                    break;
                case 4:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource5, (uint)(dataChannel << 0), 0x001f);
                    break;
                case 5:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource6, (uint)(dataChannel << 0), 0x001f);
                    break;
                case 6:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource7, (uint)(dataChannel << 0), 0x001f);
                    break;
                case 7:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource8, (uint)(dataChannel << 0), 0x001f);
                    break;
            }
            dev.UpdateWireIns();
        }

        /// <summary>
        /// Enables or disables external triggering of amplifier hardware 'fast settle' function (blanking).
        /// </summary>
        /// <param name="enable">
        /// Enables external triggering of 'fast settle' function if set to true or disables it
        /// if set to false.
        /// </param>
        /// <remarks>
        /// If external triggering is enabled, the fast settling of amplifiers on all connected
        /// chips will be controlled in real time via one of the 16 TTL inputs.
        /// </remarks>
        public void EnableExternalFastSettle(bool enable)
        {
            dev.SetWireInValue(OkEndPoint.WireInMultiUse, (uint)(enable ? 1 : 0));
            dev.UpdateWireIns();
            dev.ActivateTriggerIn(OkEndPoint.TrigInExtFastSettle, 0);
        }

        /// <summary>
        /// Selects which of the TTL inputs 0-15 is used to perform a hardware 'fast settle' (blanking)
        /// of the amplifiers if external triggering of fast settling is enabled.
        /// </summary>
        /// <param name="channel">
        /// The TTL input channel used to trigger a 'fast settle' of all connected chips.
        /// </param>
        public void SetExternalFastSettleChannel(int channel)
        {
            if (channel < 0 || channel > 15)
            {
                throw new ArgumentException("channel out of range.", "channel");
            }

            dev.SetWireInValue(OkEndPoint.WireInMultiUse, (uint)channel);
            dev.UpdateWireIns();
            dev.ActivateTriggerIn(OkEndPoint.TrigInExtFastSettle, 1);
        }

        /// <summary>
        /// Enables or disables external control of RHD2000 auxiliary digital output pin (auxout).
        /// </summary>
        /// <param name="port">
        /// The SPI port for which to enable or disable external control of RHD2000 auxiliary
        /// digital output pin.
        /// </param>
        /// <param name="enable">
        /// Enables external control of RHD2000 auxiliary digital output pin if set to true or
        /// disables it if set to false.
        /// </param>
        /// <remarks>
        /// If external control is enabled, the digital output of all chips connected to a
        /// selected SPI port will be controlled in real time via one of the 16 TTL inputs.
        /// </remarks>
        public void EnableExternalDigOut(BoardPort port, bool enable)
        {
            dev.SetWireInValue(OkEndPoint.WireInMultiUse, (uint)(enable ? 1 : 0));
            dev.UpdateWireIns();

            switch (port)
            {
                case BoardPort.PortA:
                    dev.ActivateTriggerIn(OkEndPoint.TrigInExtDigOut, 0);
                    break;
                case BoardPort.PortB:
                    dev.ActivateTriggerIn(OkEndPoint.TrigInExtDigOut, 1);
                    break;
                case BoardPort.PortC:
                    dev.ActivateTriggerIn(OkEndPoint.TrigInExtDigOut, 2);
                    break;
                case BoardPort.PortD:
                    dev.ActivateTriggerIn(OkEndPoint.TrigInExtDigOut, 3);
                    break;
                default:
                    throw new ArgumentException("port out of range.", "port");
            }
        }

        /// <summary>
        /// Selects which of the TTL inputs 0-15 is used to control the auxiliary digital output
        /// pin of the chips connected to a particular SPI port, if external control of auxout is enabled.
        /// </summary>
        /// <param name="port">
        /// Specifies the SPI port where the controlled auxiliary digital output pins are connected.
        /// </param>
        /// <param name="channel">
        /// The TTL input channel used to control the auxiliary digital output pin of the chips connected
        /// to the specified SPI port.
        /// </param>
        public void SetExternalDigOutChannel(BoardPort port, int channel)
        {
            if (channel < 0 || channel > 15)
            {
                throw new ArgumentException("channel out of range.", "channel");
            }

            dev.SetWireInValue(OkEndPoint.WireInMultiUse, (uint)channel);
            dev.UpdateWireIns();

            switch (port)
            {
                case BoardPort.PortA:
                    dev.ActivateTriggerIn(OkEndPoint.TrigInExtDigOut, 4);
                    break;
                case BoardPort.PortB:
                    dev.ActivateTriggerIn(OkEndPoint.TrigInExtDigOut, 5);
                    break;
                case BoardPort.PortC:
                    dev.ActivateTriggerIn(OkEndPoint.TrigInExtDigOut, 6);
                    break;
                case BoardPort.PortD:
                    dev.ActivateTriggerIn(OkEndPoint.TrigInExtDigOut, 7);
                    break;
                default:
                    throw new ArgumentException("port out of range.", "port");
            }
        }

        /// <summary>
        /// Enables or disables optional FPGA-implemented digital high-pass filters associated with DAC
        /// outputs on USB interface board.
        /// </summary>
        /// <param name="enable">
        /// Enables optional high-pass filters associated with DAC outputs if set to true or disables
        /// them if set to false.
        /// </param>
        /// <remarks>
        /// These one-pole filters can be used to record wideband neural data while viewing only spikes
        /// without LFPs on the DAC outputs, for example.  This is useful when using the low-latency
        /// FPGA thresholds to detect spikes and produce digital pulses on the TTL outputs, for example.
        /// </remarks>
        public void EnableDacHighpassFilter(bool enable)
        {
            dev.SetWireInValue(OkEndPoint.WireInMultiUse, (uint)(enable ? 1 : 0));
            dev.UpdateWireIns();
            dev.ActivateTriggerIn(OkEndPoint.TrigInDacHpf, 0);
        }

        /// <summary>
        /// Sets cutoff frequency (in Hz) for optional FPGA-implemented digital high-pass filters
        /// associated with DAC outputs on USB interface board.
        /// </summary>
        /// <param name="cutoff">
        /// The cutoff frequency (in Hz) for the DAC output high-pass filters.
        /// </param>
        /// <remarks>
        /// These one-pole filters can be used to record wideband neural data while viewing only spikes
        /// without LFPs on the DAC outputs, for example.  This is useful when using the low-latency
        /// FPGA thresholds to detect spikes and produce digital pulses on the TTL outputs, for example.
        /// </remarks>
        public void SetDacHighpassFilter(double cutoff)
        {
            double b;
            int filterCoefficient;

            // Note that the filter coefficient is a function of the amplifier sample rate, so this
            // function should be called after the sample rate is changed.
            b = 1.0 - Math.Exp(-2.0 * Math.PI * cutoff / GetSampleRate());

            // In hardware, the filter coefficient is represented as a 16-bit number.
            filterCoefficient = (int)Math.Floor(65536.0 * b + 0.5);

            if (filterCoefficient < 1)
            {
                filterCoefficient = 1;
            }
            else if (filterCoefficient > 65535)
            {
                filterCoefficient = 65535;
            }

            dev.SetWireInValue(OkEndPoint.WireInMultiUse, (uint)filterCoefficient);
            dev.UpdateWireIns();
            dev.ActivateTriggerIn(OkEndPoint.TrigInDacHpf, 1);
        }

        /// <summary>
        /// Sets thresholds for DAC channels; threshold output signals appear on TTL outputs 0-7.
        /// </summary>
        /// <param name="dacChannel"></param>
        /// <param name="threshold">
        /// The RHD2000 chip ADC output value, falling in the range of 0 to 65535, where the
        /// 'zero' level is 32768.
        /// </param>
        /// <param name="trigPolarity">
        /// If trigPolarity is true, voltages equaling or rising above the threshold produce a high TTL
        /// output; otherwise, voltages equaling or falling below the threshold produce a high TTL output.
        /// </param>
        public void SetDacThreshold(int dacChannel, int threshold, bool trigPolarity)
        {
            if (dacChannel < 0 || dacChannel > 7)
            {
                throw new ArgumentException("dacChannel out of range.", "dacChannel");
            }

            if (threshold < 0 || threshold > 65535)
            {
                throw new ArgumentException("threshold out of range.", "threshold");
            }

            // Set threshold level.
            dev.SetWireInValue(OkEndPoint.WireInMultiUse, (uint)threshold);
            dev.UpdateWireIns();
            dev.ActivateTriggerIn(OkEndPoint.TrigInDacThresh, dacChannel);

            // Set threshold polarity.
            dev.SetWireInValue(OkEndPoint.WireInMultiUse, (uint)(trigPolarity ? 1 : 0));
            dev.UpdateWireIns();
            dev.ActivateTriggerIn(OkEndPoint.TrigInDacThresh, dacChannel + 8);
        }

        /// <summary>
        /// Sets the TTL output mode of the board.
        /// </summary>
        /// <param name="mode">
        /// If set to 0, all 16 TTL outputs are under manual control; if set to 1,
        /// the top 8 TTL outputs are under manual control; while the bottom 8 TTL
        /// outputs are outputs of DAC comparators.
        /// </param>
        public void SetTtlMode(int mode)
        {
            if (mode < 0 || mode > 1)
            {
                throw new ArgumentException("mode out of range.", "mode");
            }

            dev.SetWireInValue(OkEndPoint.WireInResetRun, (uint)(mode << 3), 0x0008);
            dev.UpdateWireIns();
        }

        /// <summary>
        /// Flushes all remaining data out of the FIFO. This function should only be called when
        /// SPI data acquisition has been stopped.
        /// </summary>
        public void Flush()
        {
            if (usb3)
            {
                dev.SetWireInValue(OkEndPoint.WireInResetRun, 1 << 16, 1 << 16); //Override pipeout block throttle
                dev.UpdateWireIns();
                while (NumWordsInFifo() >= USB_BUFFER_SIZE / 2)
                {
                    dev.ReadFromBlockPipeOut(OkEndPoint.PipeOutData, USB3_BLOCK_SIZE, USB_BUFFER_SIZE, usbBuffer);
                }
                while (NumWordsInFifo() > 0)
                {
                    dev.ReadFromBlockPipeOut(OkEndPoint.PipeOutData, USB3_BLOCK_SIZE, USB3_BLOCK_SIZE * Math.Max(2 * (int)NumWordsInFifo() / USB3_BLOCK_SIZE, 1), usbBuffer);
                }
                dev.SetWireInValue(OkEndPoint.WireInResetRun, 0, 1 << 16);
                dev.UpdateWireIns();
            }
            else
            {
                while (NumWordsInFifo() >= USB_BUFFER_SIZE / 2)
                {
                    dev.ReadFromPipeOut(OkEndPoint.PipeOutData, USB_BUFFER_SIZE, usbBuffer);
                }
                while (NumWordsInFifo() > 0)
                {
                    dev.ReadFromPipeOut(OkEndPoint.PipeOutData, (int)(2 * NumWordsInFifo()), usbBuffer);
                }
            }
        }

        /// <summary>
        /// Reads a data block from the USB interface, if one is available, and stores the data into
        /// an <see cref="Rhd2000DataBlock"/> object.
        /// </summary>
        /// <param name="dataBlock">The <see cref="Rhd2000DataBlock"/> object used to store the data.</param>
        /// <returns>True if a data block was available, false otherwise.</returns>
        public bool ReadDataBlock(Rhd2000DataBlock dataBlock)
        {
            int numBytesToRead;

            numBytesToRead = 2 * Rhd2000DataBlock.CalculateDataBlockSizeInWords(numDataStreams, usb3);
            if (numBytesToRead > USB_BUFFER_SIZE)
            {
                throw new InvalidOperationException("USB buffer size exceeded. Increase value of USB_BUFFER_SIZE.");
            }

            if (usb3)
            {
                dev.ReadFromBlockPipeOut(OkEndPoint.PipeOutData, USB3_BLOCK_SIZE, numBytesToRead, usbBuffer);
            }
            else
            {
                dev.ReadFromPipeOut(OkEndPoint.PipeOutData, numBytesToRead, usbBuffer);
            }

            dataBlock.FillFromUsbBuffer(usbBuffer, 0);
            return true;
        }

        /// <summary>
        /// Reads a specified number of data blocks from the USB interface and appends them to <paramref name="dataQueue"/>.
        /// </summary>
        /// <param name="numBlocks">The number of blocks to read from the USB interface.</param>
        /// <param name="dataQueue">A queue of data blocks on which to append available data.</param>
        /// <returns>True if the specified number of data blocks was available, false otherwise.</returns>
        public bool ReadDataBlocks(int numBlocks, Queue<Rhd2000DataBlock> dataQueue)
        {
            int numWordsToRead, numBytesToRead;
            int i;
            Rhd2000DataBlock dataBlock;

            numWordsToRead = numBlocks * Rhd2000DataBlock.CalculateDataBlockSizeInWords(numDataStreams, usb3);
            if (NumWordsInFifo() < numWordsToRead)
            {
                return false;
            }

            numBytesToRead = 2 * numWordsToRead;
            if (numBytesToRead > USB_BUFFER_SIZE)
            {
                throw new InvalidOperationException("USB buffer size exceeded. Increase value of USB_BUFFER_SIZE.");
            }

            if (usb3)
            {
                dev.ReadFromBlockPipeOut(OkEndPoint.PipeOutData, USB3_BLOCK_SIZE, numBytesToRead, usbBuffer);
            }
            else
            {
                dev.ReadFromPipeOut(OkEndPoint.PipeOutData, numBytesToRead, usbBuffer);
            }

            for (i = 0; i < numBlocks; ++i)
            {
                dataBlock = new Rhd2000DataBlock(numDataStreams, usb3);
                dataBlock.FillFromUsbBuffer(usbBuffer, i);
                dataQueue.Enqueue(dataBlock);
            }

            return true;
        }

        /// <summary>
        /// Writes the contents of <paramref name="dataQueue"/> to a binary output stream <paramref name="saveOut"/>.
        /// </summary>
        /// <param name="dataQueue">The data block queue that will be written to the binary output stream.</param>
        /// <param name="saveOut">The binary output stream on which to write the data.</param>
        /// <returns>The number of data blocks written to the binary output stream.</returns>
        public int QueueToFile(Queue<Rhd2000DataBlock> dataQueue, Stream saveOut)
        {
            int count = 0;

            while (dataQueue.Count > 0)
            {
                dataQueue.Peek().Write(saveOut, GetNumEnabledDataStreams());
                dataQueue.Dequeue();
                ++count;
            }

            return count;
        }

        // Return name of Opal Kelly board based on model code.
        string OpalKellyModelName(okEProduct model)
        {
            switch (model)
            {
                case okEProduct.okPRODUCT_XEM3001V1:
                    return ("XEM3001V1");
                case okEProduct.okPRODUCT_XEM3001V2:
                    return ("XEM3001V2");
                case okEProduct.okPRODUCT_XEM3010:
                    return ("XEM3010");
                case okEProduct.okPRODUCT_XEM3005:
                    return ("XEM3005");
                case okEProduct.okPRODUCT_XEM3001CL:
                    return ("XEM3001CL");
                case okEProduct.okPRODUCT_XEM3020:
                    return ("XEM3020");
                case okEProduct.okPRODUCT_XEM3050:
                    return ("XEM3050");
                case okEProduct.okPRODUCT_XEM9002:
                    return ("XEM9002");
                case okEProduct.okPRODUCT_XEM3001RB:
                    return ("XEM3001RB");
                case okEProduct.okPRODUCT_XEM5010:
                    return ("XEM5010");
                case okEProduct.okPRODUCT_XEM6110LX45:
                    return ("XEM6110LX45");
                case okEProduct.okPRODUCT_XEM6001:
                    return ("XEM6001");
                case okEProduct.okPRODUCT_XEM6010LX45:
                    return ("XEM6010LX45");
                case okEProduct.okPRODUCT_XEM6010LX150:
                    return ("XEM6010LX150");
                case okEProduct.okPRODUCT_XEM6110LX150:
                    return ("XEM6110LX150");
                case okEProduct.okPRODUCT_XEM6006LX9:
                    return ("XEM6006LX9");
                case okEProduct.okPRODUCT_XEM6006LX16:
                    return ("XEM6006LX16");
                case okEProduct.okPRODUCT_XEM6006LX25:
                    return ("XEM6006LX25");
                case okEProduct.okPRODUCT_XEM5010LX110:
                    return ("XEM5010LX110");
                case okEProduct.okPRODUCT_ZEM4310:
                    return ("ZEM4310");
                case okEProduct.okPRODUCT_XEM6310LX45:
                    return ("XEM6310LX45");
                case okEProduct.okPRODUCT_XEM6310LX150:
                    return ("XEM6310LX150");
                case okEProduct.okPRODUCT_XEM6110V2LX45:
                    return ("XEM6110V2LX45");
                case okEProduct.okPRODUCT_XEM6110V2LX150:
                    return ("XEM6110V2LX150");
                case okEProduct.okPRODUCT_XEM6002LX9:
                    return ("XEM6002LX9");
                case okEProduct.okPRODUCT_XEM6310MTLX45T:
                    return ("XEM6310MTLX45T");
                case okEProduct.okPRODUCT_XEM6320LX130T:
                    return ("XEM6320LX130T");
                default:
                    return ("UNKNOWN");
            }
        }

        /// <summary>
        /// Gets the 4-bit "board mode" input.
        /// </summary>
        /// <returns>
        /// The 4-bit "board mode" input.
        /// </returns>
        public int GetBoardMode()
        {
            int mode;

            dev.UpdateWireOuts();
            mode = (int)dev.GetWireOutValue(OkEndPoint.WireOutBoardMode);

            return mode;
        }

        /// <summary>
        /// Gets the FPGA cable delay for selected SPI port.
        /// </summary>
        /// <param name="port">
        /// The SPI port for which to return the cable delay.
        /// </param>
        /// <returns>
        /// The FPGA cable delay for selected SPI port.
        /// </returns>
        public int GetCableDelay(BoardPort port)
        {
            switch (port)
            {
                case BoardPort.PortA:
                    return cableDelay[0];
                case BoardPort.PortB:
                    return cableDelay[1];
                case BoardPort.PortC:
                    return cableDelay[2];
                case BoardPort.PortD:
                    return cableDelay[3];
                default:
                    throw new ArgumentException("unknown port.", "port");
            }
        }

        /// <summary>
        /// Copies the FPGA cable delays for all SPI ports onto the specified array.
        /// </summary>
        /// <param name="delays">
        /// The array that will hold the the FPGA cable delays for all SPI ports.
        /// </param>
        public void GetCableDelay(int[] delays)
        {
            if (delays.Length != cableDelay.Length)
            {
                throw new ArgumentException("cable delay array must be of length 4.", "delays");
            }

            for (int i = 0; i < delays.Length; ++i)
            {
                delays[i] = cableDelay[i];
            }
        }

        /// <summary>
        /// Gets a value indicating whether this board uses the USB3 interface.
        /// </summary>
        /// <returns>A value indicating whether this board uses the USB3 interface.</returns>
        public bool IsUsb3()
        {
            return usb3;
        }

        // Reads system clock frequency from Opal Kelly board (in MHz).  Should be 100 MHz for normal
        // Rhythm operation.
        double GetSystemClockFreq()
        {
            // Read back the CY22393 PLL configuation
            okCPLL22393 pll = new okCPLL22393();
            dev.GetEepromPLL22393Configuration(pll);

            return pll.GetOutputFrequency(0);
        }

        // Is variable-frequency clock DCM programming done?
        bool IsDcmProgDone()
        {
            uint value;

            dev.UpdateWireOuts();
            value = dev.GetWireOutValue(OkEndPoint.WireOutDataClkLocked);

            return ((value & 0x0002) > 1);
        }

        // Is variable-frequency clock PLL locked?
        bool IsDataClockLocked()
        {
            uint value;

            dev.UpdateWireOuts();
            value = dev.GetWireOutValue(OkEndPoint.WireOutDataClkLocked);

            return ((value & 0x0001) > 0);
        }

        void IDisposable.Dispose()
        {
            Close();
        }

        /// <summary>
        /// Closes the FPGA connection and releases any resources used by the
        /// <see cref="Rhd2000EvalBoard"/>.
        /// </summary>
        public void Close()
        {
            if (!disposed)
            {
                if (dev != null)
                {
                    dev.Dispose();
                    dev = null;
                }

                disposed = true;
            }
        }
    }
}
