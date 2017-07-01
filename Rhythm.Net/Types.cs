using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Rhythm.Net
{
    /// <summary>
    /// Specifies the series capacitors available for impedance testing.
    /// </summary>
    public enum ZcheckCs
    {
        /// <summary>
        /// Specifies that a series capacitor of 100fF should be used.
        /// </summary>
        ZcheckCs100fF,

        /// <summary>
        /// Specifies that a series capacitor of 1pF should be used.
        /// </summary>
        ZcheckCs1pF,

        /// <summary>
        /// Specifies that a series capacitor of 10pF should be used.
        /// </summary>
        ZcheckCs10pF
    }

    /// <summary>
    /// Specifies the possible amplifier input polarities to use for impedance testing (RHD2216 only).
    /// </summary>
    public enum ZcheckPolarity
    {
        /// <summary>
        /// Specifies that the positive amplifier input polarity should be used.
        /// </summary>
        ZcheckPositiveInput,

        /// <summary>
        /// Specifies that the negative amplifier input polarity should be used.
        /// </summary>
        ZcheckNegativeInput
    }

    /// <summary>
    /// Specifies the available MOSI command codes.
    /// </summary>
    public enum Rhd2000CommandType
    {
        /// <summary>
        /// Specifies the MOSI convert command.
        /// </summary>
        Rhd2000CommandConvert,

        /// <summary>
        /// Specifies the MOSI calibrate command.
        /// </summary>
        Rhd2000CommandCalibrate,

        /// <summary>
        /// Specifies the MOSI calibration clear command.
        /// </summary>
        Rhd2000CommandCalClear,

        /// <summary>
        /// Specifies the MOSI register write command.
        /// </summary>
        Rhd2000CommandRegWrite,

        /// <summary>
        /// Specifies the MOSI register read command.
        /// </summary>
        Rhd2000CommandRegRead
    }

    /// <summary>
    /// Specifies the available per-channel sampling rates.
    /// </summary>
    public enum AmplifierSampleRate
    {
        /// <summary>
        /// Specifies a per-channel sampling rate of 1000Hz.
        /// </summary>
        SampleRate1000Hz,

        /// <summary>
        /// Specifies a per-channel sampling rate of 1250Hz.
        /// </summary>
        SampleRate1250Hz,

        /// <summary>
        /// Specifies a per-channel sampling rate of 1500Hz.
        /// </summary>
        SampleRate1500Hz,

        /// <summary>
        /// Specifies a per-channel sampling rate of 2000Hz.
        /// </summary>
        SampleRate2000Hz,

        /// <summary>
        /// Specifies a per-channel sampling rate of 2500Hz.
        /// </summary>
        SampleRate2500Hz,

        /// <summary>
        /// Specifies a per-channel sampling rate of 3000Hz.
        /// </summary>
        SampleRate3000Hz,

        /// <summary>
        /// Specifies a per-channel sampling rate of 3333Hz.
        /// </summary>
        SampleRate3333Hz,

        /// <summary>
        /// Specifies a per-channel sampling rate of 4000Hz.
        /// </summary>
        SampleRate4000Hz,

        /// <summary>
        /// Specifies a per-channel sampling rate of 5000Hz.
        /// </summary>
        SampleRate5000Hz,

        /// <summary>
        /// Specifies a per-channel sampling rate of 6250Hz.
        /// </summary>
        SampleRate6250Hz,

        /// <summary>
        /// Specifies a per-channel sampling rate of 8000Hz.
        /// </summary>
        SampleRate8000Hz,

        /// <summary>
        /// Specifies a per-channel sampling rate of 10000Hz.
        /// </summary>
        SampleRate10000Hz,

        /// <summary>
        /// Specifies a per-channel sampling rate of 12500Hz.
        /// </summary>
        SampleRate12500Hz,

        /// <summary>
        /// Specifies a per-channel sampling rate of 15000Hz.
        /// </summary>
        SampleRate15000Hz,

        /// <summary>
        /// Specifies a per-channel sampling rate of 20000Hz.
        /// </summary>
        SampleRate20000Hz,

        /// <summary>
        /// Specifies a per-channel sampling rate of 25000Hz.
        /// </summary>
        SampleRate25000Hz,

        /// <summary>
        /// Specifies a per-channel sampling rate of 30000Hz.
        /// </summary>
        SampleRate30000Hz
    }

    /// <summary>
    /// Specifies the available auxiliary command slots for SPI ports.
    /// </summary>
    public enum AuxCmdSlot
    {
        /// <summary>
        /// Specifies the auxiliary command slot 1.
        /// </summary>
        AuxCmd1,

        /// <summary>
        /// Specifies the auxiliary command slot 2.
        /// </summary>
        AuxCmd2,

        /// <summary>
        /// Specifies the auxiliary command slot 3.
        /// </summary>
        AuxCmd3
    }

    /// <summary>
    /// Specifies the available board SPI ports.
    /// </summary>
    public enum BoardPort
    {
        /// <summary>
        /// Specifies the board SPI port A.
        /// </summary>
        PortA,

        /// <summary>
        /// Specifies the board SPI port B.
        /// </summary>
        PortB,

        /// <summary>
        /// Specifies the board SPI port C.
        /// </summary>
        PortC,

        /// <summary>
        /// Specifies the board SPI port D.
        /// </summary>
        PortD
    }

    /// <summary>
    /// Specifies the available board SPI data sources for each port. The DDR (double data rate)
    /// sources are included to support future 64-channel RHD2000 chips that return MISO data
    /// on both the rising and falling edges of SCLK.
    /// </summary>
    public enum BoardDataSource
    {
        /// <summary>
        /// Specifies the board SPI port A1 data source.
        /// </summary>
        PortA1 = 0,

        /// <summary>
        /// Specifies the board SPI port A2 data source.
        /// </summary>
        PortA2 = 1,

        /// <summary>
        /// Specifies the board SPI port B1 data source.
        /// </summary>
        PortB1 = 2,

        /// <summary>
        /// Specifies the board SPI port B2 data source.
        /// </summary>
        PortB2 = 3,

        /// <summary>
        /// Specifies the board SPI port C1 data source.
        /// </summary>
        PortC1 = 4,

        /// <summary>
        /// Specifies the board SPI port C2 data source.
        /// </summary>
        PortC2 = 5,

        /// <summary>
        /// Specifies the board SPI port D1 data source.
        /// </summary>
        PortD1 = 6,

        /// <summary>
        /// Specifies the board SPI port D2 data source.
        /// </summary>
        PortD2 = 7,

        /// <summary>
        /// Specifies the board SPI port A1 DDR data source.
        /// </summary>
        PortA1Ddr = 8,

        /// <summary>
        /// Specifies the board SPI port A2 DDR data source.
        /// </summary>
        PortA2Ddr = 9,

        /// <summary>
        /// Specifies the board SPI port B1 DDR data source.
        /// </summary>
        PortB1Ddr = 10,

        /// <summary>
        /// Specifies the board SPI port B2 DDR data source.
        /// </summary>
        PortB2Ddr = 11,

        /// <summary>
        /// Specifies the board SPI port C1 DDR data source.
        /// </summary>
        PortC1Ddr = 12,

        /// <summary>
        /// Specifies the board SPI port C2 DDR data source.
        /// </summary>
        PortC2Ddr = 13,

        /// <summary>
        /// Specifies the board SPI port D1 DDR data source.
        /// </summary>
        PortD1Ddr = 14,

        /// <summary>
        /// Specifies the board SPI port D2 DDR data source.
        /// </summary>
        PortD2Ddr = 15
    }

    // Opal Kelly module USB interface endpoint addresses
    static class OkEndPoint
    {
        public const int WireInResetRun = 0x00;
        public const int WireInMaxTimeStepLsb = 0x01;
        public const int WireInMaxTimeStepMsb = 0x02;
        public const int WireInDataFreqPll = 0x03;
        public const int WireInMisoDelay = 0x04;
        public const int WireInCmdRamAddr = 0x05;
        public const int WireInCmdRamBank = 0x06;
        public const int WireInCmdRamData = 0x07;
        public const int WireInAuxCmdBank1 = 0x08;
        public const int WireInAuxCmdBank2 = 0x09;
        public const int WireInAuxCmdBank3 = 0x0a;
        public const int WireInAuxCmdLength1 = 0x0b;
        public const int WireInAuxCmdLength2 = 0x0c;
        public const int WireInAuxCmdLength3 = 0x0d;
        public const int WireInAuxCmdLoop1 = 0x0e;
        public const int WireInAuxCmdLoop2 = 0x0f;
        public const int WireInAuxCmdLoop3 = 0x10;
        public const int WireInLedDisplay = 0x11;
        public const int WireInDataStreamSel1234 = 0x12;
        public const int WireInDataStreamSel5678 = 0x13;
        public const int WireInDataStreamEn = 0x14;
        public const int WireInTtlOut = 0x15;
        public const int WireInDacSource1 = 0x16;
        public const int WireInDacSource2 = 0x17;
        public const int WireInDacSource3 = 0x18;
        public const int WireInDacSource4 = 0x19;
        public const int WireInDacSource5 = 0x1a;
        public const int WireInDacSource6 = 0x1b;
        public const int WireInDacSource7 = 0x1c;
        public const int WireInDacSource8 = 0x1d;
        public const int WireInDacManual = 0x1e;
        public const int WireInMultiUse = 0x1f;

        public const int TrigInDcmProg = 0x40;
        public const int TrigInSpiStart = 0x41;
        public const int TrigInRamWrite = 0x42;
        public const int TrigInDacThresh = 0x43;
        public const int TrigInDacHpf = 0x44;
        public const int TrigInExtFastSettle = 0x45;
        public const int TrigInExtDigOut = 0x46;
        public const int TrigInOpenEphys = 0x5a;

        public const int WireOutNumWordsLsb = 0x20;
        public const int WireOutNumWordsMsb = 0x21;
        public const int WireOutSpiRunning = 0x22;
        public const int WireOutTtlIn = 0x23;
        public const int WireOutDataClkLocked = 0x24;
        public const int WireOutBoardMode = 0x25;
        public const int WireOutBoardId = 0x3e;
        public const int WireOutBoardVersion = 0x3f;

        public const int PipeOutData = 0xa0;
    }
}
