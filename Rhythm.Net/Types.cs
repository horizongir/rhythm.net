using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Rhythm.Net
{
    public enum ZcheckCs
    {
        ZcheckCs100fF,
        ZcheckCs1pF,
        ZcheckCs10pF
    };

    public enum ZcheckPolarity
    {
        ZcheckPositiveInput,
        ZcheckNegativeInput
    };

    public enum Rhd2000CommandType
    {
        Rhd2000CommandConvert,
        Rhd2000CommandCalibrate,
        Rhd2000CommandCalClear,
        Rhd2000CommandRegWrite,
        Rhd2000CommandRegRead
    };

    public enum AmplifierSampleRate
    {
        SampleRate1000Hz,
        SampleRate1250Hz,
        SampleRate1500Hz,
        SampleRate2000Hz,
        SampleRate2500Hz,
        SampleRate3000Hz,
        SampleRate3333Hz,
        SampleRate4000Hz,
        SampleRate5000Hz,
        SampleRate6250Hz,
        SampleRate8000Hz,
        SampleRate10000Hz,
        SampleRate12500Hz,
        SampleRate15000Hz,
        SampleRate20000Hz,
        SampleRate25000Hz,
        SampleRate30000Hz
    };

    public enum AuxCmdSlot
    {
        AuxCmd1,
        AuxCmd2,
        AuxCmd3
    };

    public enum BoardPort
    {
        PortA,
        PortB,
        PortC,
        PortD
    };

    public enum BoardDataSource
    {
        PortA1 = 0,
        PortA2 = 1,
        PortB1 = 2,
        PortB2 = 3,
        PortC1 = 4,
        PortC2 = 5,
        PortD1 = 6,
        PortD2 = 7,
        PortA1Ddr = 8,
        PortA2Ddr = 9,
        PortB1Ddr = 10,
        PortB2Ddr = 11,
        PortC1Ddr = 12,
        PortC2Ddr = 13,
        PortD1Ddr = 14,
        PortD2Ddr = 15
    };

    public enum DacManual
    {
        DacManual1,
        DacManual2
    };

    // Opal Kelly module USB interface endpoint addresses
    public static class OkEndPoint
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
        public const int WireInDacManual1 = 0x1e;
        public const int WireInDacManual2 = 0x1f;

        public const int TrigInDcmProg = 0x40;
        public const int TrigInSpiStart = 0x41;
        public const int TrigInRamWrite = 0x42;

        public const int WireOutNumWordsLsb = 0x20;
        public const int WireOutNumWordsMsb = 0x21;
        public const int WireOutSpiRunning = 0x22;
        public const int WireOutTtlIn = 0x23;
        public const int WireOutDataClkLocked = 0x24;
        public const int WireOutBoardId = 0x3e;
        public const int WireOutBoardVersion = 0x3f;

        public const int PipeOutData = 0xa0;
    };
}
