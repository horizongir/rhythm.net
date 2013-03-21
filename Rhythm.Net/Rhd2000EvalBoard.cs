using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using OpalKelly.FrontPanel;

namespace Rhythm.Net
{
    public class Rhd2000EvalBoard
    {
        const int USB_BUFFER_SIZE = 360000;
        const int MAX_NUM_DATA_STREAMS = 8;
        const uint RHYTHM_BOARD_ID = 500;

        okCFrontPanel dev;
        AmplifierSampleRate sampleRate;
        int numDataStreams; // total number of data streams currently enabled
        int[] dataStreamEnabled = new int[MAX_NUM_DATA_STREAMS]; // 0 (disabled) or 1 (enabled)

        // Buffer for reading bytes from USB interface
        byte[] usbBuffer = new byte[USB_BUFFER_SIZE];

        // This class provides access to and control of the Opal Kelly XEM6010 USB/FPGA
        // interface board running the Rhythm interface Verilog code.

        // Constructor.  Set sampling rate variable to 30.0 kS/s/channel (FPGA default).
        public Rhd2000EvalBoard()
        {
            int i;
            sampleRate = AmplifierSampleRate.SampleRate30000Hz; // Rhythm FPGA boots up with 30.0 kS/s/channel sampling rate
            numDataStreams = 0;

            for (i = 0; i < MAX_NUM_DATA_STREAMS; ++i)
            {
                dataStreamEnabled[i] = 0;
            }
        }

        // Find an Opal Kelly XEM6010-LX45 board attached to a USB port and open it.
        // Returns true if successful.
        bool open()
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
                if (dev.GetDeviceListModel(i) == okCFrontPanel.BoardModel.brdXEM6010LX45)
                {
                    serialNumber = dev.GetDeviceListSerial(i);
                    break;
                }
            }

            // Attempt to open device.
            if (dev.OpenBySerial(serialNumber) != okCFrontPanel.ErrorCode.NoError)
            {
                dev.Dispose();
                throw new InvalidOperationException("Device could not be opened.  Is one connected?");
            }

            // Configure the on-board PLL appropriately.
            dev.LoadDefaultPLLConfiguration();

            // Get some general information about the XEM.
            Console.WriteLine("FPGA system clock: " + getSystemClockFreq() + " MHz"); // Should indicate 100 MHz
            Console.WriteLine("Opal Kelly device firmware version: " + dev.GetDeviceMajorVersion() + "." +
                    dev.GetDeviceMinorVersion());
            Console.WriteLine("Opal Kelly device serial number: " + dev.GetSerialNumber());
            Console.WriteLine("Opal Kelly device ID string: " + dev.GetDeviceID());

            return (true);
        }

        // Uploads the configuration file (bitfile) to the FPGA.  Returns true if successful.
        void uploadFpgaBitfile(string filename)
        {
            okCFrontPanel.ErrorCode errorCode = dev.ConfigureFPGA(filename);

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

            if (boardId != RHYTHM_BOARD_ID)
            {
                throw new InvalidOperationException("FPGA configuration does not support Rhythm.  Incorrect board ID: " + boardId);
            }
            else
            {
                Console.WriteLine("Rhythm configuration file successfully loaded.  Rhythm version number: " + boardVersion + Environment.NewLine);
            }
        }

        // Initialize Rhythm FPGA to default starting values.
        void initialize()
        {
            int i;

            resetBoard();
            setSampleRate(AmplifierSampleRate.SampleRate30000Hz);
            selectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd1, 0);
            selectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd1, 0);
            selectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd1, 0);
            selectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd1, 0);
            selectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd2, 0);
            selectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd2, 0);
            selectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd2, 0);
            selectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd2, 0);
            selectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd3, 0);
            selectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd3, 0);
            selectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd3, 0);
            selectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd3, 0);
            selectAuxCommandLength(AuxCmdSlot.AuxCmd1, 0, 0);
            selectAuxCommandLength(AuxCmdSlot.AuxCmd2, 0, 0);
            selectAuxCommandLength(AuxCmdSlot.AuxCmd3, 0, 0);
            setContinuousRunMode(true);
            setMaxTimeStep(4294967295);  // 4294967295 == (2^32 - 1)

            setCableLengthFeet(BoardPort.PortA, 3.0);  // assume 3 ft cables
            setCableLengthFeet(BoardPort.PortB, 3.0);
            setCableLengthFeet(BoardPort.PortC, 3.0);
            setCableLengthFeet(BoardPort.PortD, 3.0);

            clearDspSettle();

            setDataSource(0, BoardDataSource.PortA1);
            setDataSource(1, BoardDataSource.PortB1);
            setDataSource(2, BoardDataSource.PortC1);
            setDataSource(3, BoardDataSource.PortD1);
            setDataSource(4, BoardDataSource.PortA2);
            setDataSource(5, BoardDataSource.PortB2);
            setDataSource(6, BoardDataSource.PortC2);
            setDataSource(7, BoardDataSource.PortD2);

            enableDataStream(0);        // start with only one data stream enabled
            for (i = 1; i < MAX_NUM_DATA_STREAMS; i++)
            {
                disableDataStream(i);
            }

            clearTtlOut();

            disableDac(0);
            disableDac(1);
            disableDac(2);
            disableDac(3);
            disableDac(4);
            disableDac(5);
            disableDac(6);
            disableDac(7);
            selectDacDataStream(0, 0);
            selectDacDataStream(1, 0);
            selectDacDataStream(2, 0);
            selectDacDataStream(3, 0);
            selectDacDataStream(4, 0);
            selectDacDataStream(5, 0);
            selectDacDataStream(6, 0);
            selectDacDataStream(7, 0);
            selectDacDataChannel(0, 0);
            selectDacDataChannel(1, 0);
            selectDacDataChannel(2, 0);
            selectDacDataChannel(3, 0);
            selectDacDataChannel(4, 0);
            selectDacDataChannel(5, 0);
            selectDacDataChannel(6, 0);
            selectDacDataChannel(7, 0);

            setDacManual(DacManual.DacManual1, 32768);    // midrange value = 0 V
            setDacManual(DacManual.DacManual2, 32768);    // midrange value = 0 V
        }

        // Set the per-channel sampling rate of the RHD2000 chips connected to the FPGA.
        bool setSampleRate(AmplifierSampleRate newSampleRate)
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
                    return (false);
            }

            sampleRate = newSampleRate;

            // Wait for DcmProgDone = 1 before reprogramming clock synthesizer
            while (isDcmProgDone() == false) { }

            // Reprogram clock synthesizer
            dev.SetWireInValue(OkEndPoint.WireInDataFreqPll, (uint)(256 * M + D));
            dev.UpdateWireIns();
            dev.ActivateTriggerIn(OkEndPoint.TrigInDcmProg, 0);

            // Wait for DataClkLocked = 1 before allowing data acquisition to continue
            while (isDataClockLocked() == false) { }

            return (true);
        }

        // Returns the current per-channel sampling rate (in Hz) as a floating-point number.
        double getSampleRate()
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

        // Upload an auxiliary command list to a particular command slot (AuxCmd1, AuxCmd2, or AuxCmd3) and RAM bank (0-15)
        // on the FPGA.
        void uploadCommandList(List<int> commandList, AuxCmdSlot auxCommandSlot, uint bank)
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

        // Print a command list to the console in readable form.
        void printCommandList(List<int> commandList)
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

        // Select an auxiliary command slot (AuxCmd1, AuxCmd2, or AuxCmd3) and bank (0-15) for a particular SPI port
        // (PortA, PortB, PortC, or PortD) on the FPGA.
        void selectAuxCommandBank(BoardPort port, AuxCmdSlot auxCommandSlot, int bank)
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

        // Specify a command sequence length (endIndex = 0-1023) and command loop index (0-1023) for a particular
        // auxiliary command slot (AuxCmd1, AuxCmd2, or AuxCmd3).
        void selectAuxCommandLength(AuxCmdSlot auxCommandSlot, int loopIndex, int endIndex)
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

        // Reset FPGA.  This clears all auxiliary command RAM banks, clears the USB FIFO, and resets the
        // per-channel sampling rate to 30.0 kS/s/ch.
        void resetBoard()
        {
            dev.SetWireInValue(OkEndPoint.WireInResetRun, 0x01, 0x01);
            dev.UpdateWireIns();
            dev.SetWireInValue(OkEndPoint.WireInResetRun, 0x00, 0x01);
            dev.UpdateWireIns();
        }

        // Set the FPGA to run continuously once started (if continuousMode == true) or to run until
        // maxTimeStep is reached (if continuousMode == false).
        void setContinuousRunMode(bool continuousMode)
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

        // Set maxTimeStep for cases where continuousMode == false.
        void setMaxTimeStep(uint maxTimeStep)
        {
            uint maxTimeStepLsb, maxTimeStepMsb;

            maxTimeStepLsb = maxTimeStep & 0x0000ffff;
            maxTimeStepMsb = maxTimeStep & 0xffff0000;

            dev.SetWireInValue(OkEndPoint.WireInMaxTimeStepLsb, maxTimeStepLsb);
            dev.SetWireInValue(OkEndPoint.WireInMaxTimeStepMsb, maxTimeStepMsb >> 16);
            dev.UpdateWireIns();
        }

        // Initiate SPI data acquisition.
        void run()
        {
            dev.ActivateTriggerIn(OkEndPoint.TrigInSpiStart, 0);
        }

        // Is the FPGA currently running?
        bool isRunning()
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

        // Returns the number of 16-bit words in the USB FIFO.  The user should never attempt to read
        // more data than the FIFO currently contains, as it is not protected against underflow.
        uint numWordsInFifo()
        {
            dev.UpdateWireOuts();
            return (dev.GetWireOutValue(OkEndPoint.WireOutNumWordsMsb) << 16) + dev.GetWireOutValue(OkEndPoint.WireOutNumWordsLsb);
        }

        // Set the delay for sampling the MISO line on a particular SPI port (PortA - PortD), in integer clock
        // steps, where each clock step is 1/2800 of a per-channel sampling period.
        // Note: Cable delay must be updated after sampleRate is changed, since cable delay calculations are
        // based on the clock frequency!
        void setCableDelay(BoardPort port, int delay)
        {
            int bitShift;

            if (delay < 0 || delay > 15)
            {
                throw new ArgumentException("delay out of range.", "delay");
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

            dev.SetWireInValue(OkEndPoint.WireInMisoDelay, (uint)(delay << bitShift), (uint)(0x000f << bitShift));
            dev.UpdateWireIns();
        }

        // Set the delay for sampling the MISO line on a particular SPI port (PortA - PortD) based on the length
        // of the cable between the FPGA and the RHD2000 chip (in meters).
        // Note: Cable delay must be updated after sampleRate is changed, since cable delay calculations are
        // based on the clock frequency!
        void setCableLengthMeters(BoardPort port, double lengthInMeters)
        {
            int delay;
            double tStep, cableVelocity, distance, timeDelay;
            const double speedOfLight = 299792458.0;  // units = meters per second
            const double xilinxLvdsOutputDelay = 1.9e-9;    // 1.9 ns Xilinx LVDS output pin delay
            const double xilinxLvdsInputDelay = 1.4e-9;     // 1.4 ns Xilinx LVDS input pin delay
            const double rhd2000Delay = 9.0e-9;             // 9.0 ns RHD2000 SCLK-to-MISO delay
            const double misoSettleTime = 10.0e-9;          // 10.0 ns delay after MISO changes, before we sample it

            tStep = 1.0 / (2800.0 * getSampleRate());  // data clock that samples MISO has a rate 35 x 80 = 2800x higher than the sampling rate
            cableVelocity = 0.67 * speedOfLight;  // propogation velocity on cable is rougly 2/3 the speed of light
            distance = 2.0 * lengthInMeters;      // round trip distance data must travel on cable
            timeDelay = distance / cableVelocity + xilinxLvdsOutputDelay + rhd2000Delay + xilinxLvdsInputDelay + misoSettleTime;

            delay = (int)Math.Ceiling(timeDelay / tStep);

            // cout << "Total delay = " << (1e9 * timeDelay) << " ns" << endl;
            // cout << "setCableLength: setting delay to " << delay << endl;

            setCableDelay(port, delay);
        }

        // Same function as above, but accepts lengths in feet instead of meters
        void setCableLengthFeet(BoardPort port, double lengthInFeet)
        {
            setCableLengthMeters(port, 0.03048 * lengthInFeet);   // convert feet to meters
        }

        // Turn on DSP settle function in the FPGA.  (Only executes when CONVERT commands are sent.)
        void setDspSettle()
        {
            dev.SetWireInValue(OkEndPoint.WireInResetRun, 0x04, 0x04);
            dev.UpdateWireIns();
        }

        // Turn off DSP settle function in the FPGA.
        void clearDspSettle()
        {
            dev.SetWireInValue(OkEndPoint.WireInResetRun, 0x00, 0x04);
            dev.UpdateWireIns();
        }

        // Assign a particular data source (e.g., PortA1, PortA2, PortB1,...) to one of the eight
        // available USB data streams (0-7).
        void setDataSource(int stream, BoardDataSource dataSource)
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
                default:
                    throw new ArgumentException("stream out of range.", "stream");
            }

            dev.SetWireInValue(endPoint, (uint)((int)dataSource << bitShift), (uint)(0x000f << bitShift));
            dev.UpdateWireIns();
        }

        // Disable one of the eight available USB data streams (0-7) to reduce USB and FIFO usage.
        void disableDataStream(int stream)
        {
            if (stream < 0 || stream > (MAX_NUM_DATA_STREAMS - 1))
            {
                throw new ArgumentException("stream out of range.", "stream");
            }

            if (dataStreamEnabled[stream] == 1)
            {
                dev.SetWireInValue(OkEndPoint.WireInDataStreamEn, (uint)(0x0000 << stream), (uint)(0xfff0 | (0x0001 << stream)));
                dev.UpdateWireIns();
                dataStreamEnabled[stream] = 0;
                numDataStreams--;
            }
        }

        // Enable one of the eight available USB data streams (0-7).
        void enableDataStream(int stream)
        {
            if (stream < 0 || stream > (MAX_NUM_DATA_STREAMS - 1))
            {
                throw new ArgumentException("stream out of range.", "stream");
            }

            if (dataStreamEnabled[stream] == 0)
            {
                dev.SetWireInValue(OkEndPoint.WireInDataStreamEn, (uint)(0x0001 << stream), (uint)(0xfff0 | (0x0001 << stream)));
                dev.UpdateWireIns();
                dataStreamEnabled[stream] = 1;
                ++numDataStreams;
            }
        }

        // Returns the number of enabled data streams.
        int getNumEnabledDataStreams()
        {
            return numDataStreams;
        }

        // Set all 16 bits of the digital TTL output lines on the FPGA to zero.
        void clearTtlOut()
        {
            dev.SetWireInValue(OkEndPoint.WireInTtlOut, 0x0000);
            dev.UpdateWireIns();
        }

        // Set the 16 bits of the digital TTL output lines on the FPGA high or low according to integer array.
        void setTtlOut(int[] ttlOutArray)
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

        // Read the 16 bits of the digital TTL input lines on the FPGA into an integer array.
        void getTtlIn(int[] ttlInArray)
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

        void setDacManual(DacManual dac, int value)
        {
            if (value < 0 || value > 65535)
            {
                throw new ArgumentException("value out of range.", "value");
            }

            switch (dac)
            {
                case DacManual.DacManual1:
                    dev.SetWireInValue(OkEndPoint.WireInDacManual1, (uint)value);
                    break;
                case DacManual.DacManual2:
                    dev.SetWireInValue(OkEndPoint.WireInDacManual2, (uint)value);
                    break;
            }
            dev.UpdateWireIns();
        }

        // Set the eight red LEDs on the XEM6010 board according to integer array.
        void setLedDisplay(int[] ledArray)
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

        // Enable AD5662 DAC channel (0-7)
        void enableDac(int dacChannel)
        {
            if (dacChannel < 0 || dacChannel > 7)
            {
                throw new ArgumentException("dacChannel out of range.", "dacChannel");
            }

            switch (dacChannel)
            {
                case 0:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource1, 0x0200, 0x0200);
                    break;
                case 1:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource2, 0x0200, 0x0200);
                    break;
                case 2:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource3, 0x0200, 0x0200);
                    break;
                case 3:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource4, 0x0200, 0x0200);
                    break;
                case 4:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource5, 0x0200, 0x0200);
                    break;
                case 5:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource6, 0x0200, 0x0200);
                    break;
                case 6:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource7, 0x0200, 0x0200);
                    break;
                case 7:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource8, 0x0200, 0x0200);
                    break;
            }
            dev.UpdateWireIns();
        }

        // Disable AD5662 DAC channel (0-7)
        void disableDac(int dacChannel)
        {
            if (dacChannel < 0 || dacChannel > 7)
            {
                throw new ArgumentException("dacChannel out of range.", "dacChannel");
            }

            switch (dacChannel)
            {
                case 0:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource1, 0x0000, 0x0200);
                    break;
                case 1:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource2, 0x0000, 0x0200);
                    break;
                case 2:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource3, 0x0000, 0x0200);
                    break;
                case 3:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource4, 0x0000, 0x0200);
                    break;
                case 4:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource5, 0x0000, 0x0200);
                    break;
                case 5:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource6, 0x0000, 0x0200);
                    break;
                case 6:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource7, 0x0000, 0x0200);
                    break;
                case 7:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource8, 0x0000, 0x0200);
                    break;
            }
            dev.UpdateWireIns();
        }

        // Assign a particular data stream (0-7) to a DAC channel (0-7).
        void selectDacDataStream(int dacChannel, int stream)
        {
            if (dacChannel < 0 || dacChannel > 7)
            {
                throw new ArgumentException("dacChannel out of range.", "dacChannel");
            }

            if (stream < 0 || stream > 9)
            {
                throw new ArgumentException("stream out of range.", "stream");
            }

            switch (dacChannel)
            {
                case 0:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource1, (uint)(stream << 5), 0x01e0);
                    break;
                case 1:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource2, (uint)(stream << 5), 0x01e0);
                    break;
                case 2:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource3, (uint)(stream << 5), 0x01e0);
                    break;
                case 3:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource4, (uint)(stream << 5), 0x01e0);
                    break;
                case 4:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource5, (uint)(stream << 5), 0x01e0);
                    break;
                case 5:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource6, (uint)(stream << 5), 0x01e0);
                    break;
                case 6:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource7, (uint)(stream << 5), 0x01e0);
                    break;
                case 7:
                    dev.SetWireInValue(OkEndPoint.WireInDacSource8, (uint)(stream << 5), 0x01e0);
                    break;
            }
            dev.UpdateWireIns();
        }

        // Assign a particular amplifier channel (0-31) to a DAC channel (0-7).
        void selectDacDataChannel(int dacChannel, int dataChannel)
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

        // Flush all remaining data out of the FIFO.  (This function should only be called when SPI
        // data acquisition has been stopped.)
        void flush()
        {
            while (numWordsInFifo() >= USB_BUFFER_SIZE / 2)
            {
                dev.ReadFromPipeOut(OkEndPoint.PipeOutData, USB_BUFFER_SIZE, usbBuffer);
            }
            while (numWordsInFifo() > 0)
            {
                dev.ReadFromPipeOut(OkEndPoint.PipeOutData, (int)(2 * numWordsInFifo()), usbBuffer);
            }
        }

        // Read data block from the USB interface, if one is available.  Returns true if data block
        // was available.
        bool readDataBlock(Rhd2000DataBlock dataBlock)
        {
            int numBytesToRead;

            numBytesToRead = 2 * Rhd2000DataBlock.calculateDataBlockSizeInWords(numDataStreams);
            if (numBytesToRead > USB_BUFFER_SIZE)
            {
                throw new InvalidOperationException("USB buffer size exceeded. Increase value of USB_BUFFER_SIZE.");
            }

            dev.ReadFromPipeOut(OkEndPoint.PipeOutData, numBytesToRead, usbBuffer);
            dataBlock.fillFromUsbBuffer(usbBuffer, 0, numDataStreams);
            return true;
        }

        // Reads a certain number of USB data blocks, if the specified number is available, and appends them
        // to queue.  Returns true if data blocks were available.
        bool readDataBlocks(int numBlocks, Queue<Rhd2000DataBlock> dataQueue)
        {
            int numWordsToRead, numBytesToRead;
            int i;
            Rhd2000DataBlock dataBlock;

            numWordsToRead = numBlocks * Rhd2000DataBlock.calculateDataBlockSizeInWords(numDataStreams);
            if (numWordsInFifo() < numWordsToRead)
            {
                return false;
            }

            numBytesToRead = 2 * numWordsToRead;
            if (numBytesToRead > USB_BUFFER_SIZE)
            {
                throw new InvalidOperationException("USB buffer size exceeded. Increase value of USB_BUFFER_SIZE.");
            }

            dev.ReadFromPipeOut(OkEndPoint.PipeOutData, numBytesToRead, usbBuffer);
            for (i = 0; i < numBlocks; ++i)
            {
                dataBlock = new Rhd2000DataBlock(numDataStreams);
                dataBlock.fillFromUsbBuffer(usbBuffer, i, numDataStreams);
                dataQueue.Enqueue(dataBlock);
            }

            return true;
        }

        // Writes the contents of a data block queue (dataQueue) to a binary output stream (saveOut).
        // Returns the number of data blocks written.
        int queueToFile(Queue<Rhd2000DataBlock> dataQueue, Stream saveOut)
        {
            int count = 0;

            while (dataQueue.Count > 0)
            {
                dataQueue.Peek().write(saveOut, getNumEnabledDataStreams());
                dataQueue.Dequeue();
                ++count;
            }

            return count;
        }

        // Return name of Opal Kelly board based on model code.
        string opalKellyModelName(okEProduct model)
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

        // Reads system clock frequency from Opal Kelly board (in MHz).  Should be 100 MHz for normal
        // Rhythm operation.
        double getSystemClockFreq()
        {
            // Read back the CY22393 PLL configuation
            okCPLL22393 pll = new okCPLL22393();
            dev.GetEepromPLL22393Configuration(pll);

            return pll.GetOutputFrequency(0);
        }

        // Is variable-frequency clock DCM programming done?
        bool isDcmProgDone()
        {
            uint value;

            dev.UpdateWireOuts();
            value = dev.GetWireOutValue(OkEndPoint.WireOutDataClkLocked);

            return ((value & 0x0002) > 1);
        }

        // Is variable-frequency clock PLL locked?
        bool isDataClockLocked()
        {
            uint value;

            dev.UpdateWireOuts();
            value = dev.GetWireOutValue(OkEndPoint.WireOutDataClkLocked);

            return ((value & 0x0001) > 0);
        }
    }
}
