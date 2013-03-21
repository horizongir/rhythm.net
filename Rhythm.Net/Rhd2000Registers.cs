using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Rhythm.Net
{
    public class Rhd2000Registers
    {
        double sampleRate;

        // RHD2000 Register 0 variables
        int adcReferenceBw;
        int ampFastSettle;
        int ampVrefEnable;
        int adcComparatorBias;
        int adcComparatorSelect;

        // RHD2000 Register 1 variables
        int vddSenseEnable;
        int adcBufferBias;

        // RHD2000 Register 2 variables
        int muxBias;

        // RHD2000 Register 3 variables
        int muxLoad;
        int tempS1;
        int tempS2;
        int tempEn;
        int digOutHiZ;
        int digOut;

        // RHD2000 Register 4 variables
        int weakMiso;
        int twosComp;
        int absMode;
        int dspEn;
        int dspCutoffFreq;

        // RHD2000 Register 5 variables
        int zcheckDacPower;
        int zcheckLoad;
        int zcheckScale;
        int zcheckConnAll;
        int zcheckSelPol;
        int zcheckEn;

        // RHD2000 Register 6 variables
        //int zcheckDac;     // handle Zcheck DAC waveform elsewhere

        // RHD2000 Register 7 variables
        int zcheckSelect;

        // RHD2000 Register 8-13 variables
        int offChipRH1;
        int offChipRH2;
        int offChipRL;
        int adcAux1En;
        int adcAux2En;
        int adcAux3En;
        int rH1Dac1;
        int rH1Dac2;
        int rH2Dac1;
        int rH2Dac2;
        int rLDac1;
        int rLDac2;
        int rLDac3;

        // RHD2000 Register 14-17 variables
        int[] aPwr = new int[32];

        const int MaxCommandLength = 1024; // size of on-FPGA auxiliary command RAM banks

        public Rhd2000Registers(double sampleRate)
        {
            DefineSampleRate(sampleRate);

            // Set default values for all register settings
            adcReferenceBw = 3;         // ADC reference generator bandwidth (0 [highest BW] - 3 [lowest BW]);
            // always set to 3
            SetFastSettle(false);       // amplifier fast settle (off = normal operation)
            ampVrefEnable = 1;          // enable amplifier voltage references (0 = power down; 1 = enable);
            // 1 = normal operation
            adcComparatorBias = 3;      // ADC comparator preamp bias current (0 [lowest] - 3 [highest], only
            // valid for comparator select = 2,3); always set to 3
            adcComparatorSelect = 2;    // ADC comparator select; always set to 2

            vddSenseEnable = 1;         // supply voltage sensor enable (0 = disable; 1 = enable)
            // adcBufferBias = 32;      // ADC reference buffer bias current (0 [highest current] - 63 [lowest current]);
            // This value should be set according to ADC sampling rate; set in setSampleRate()

            // muxBias = 40;            // ADC input MUX bias current (0 [highest current] - 63 [lowest current]);
            // This value should be set according to ADC sampling rate; set in setSampleRate()

            // muxLoad = 0;             // MUX capacitance load at ADC input (0 [min CL] - 7 [max CL]); LSB = 3 pF
            // Set in setSampleRate()

            tempS1 = 0;                 // temperature sensor S1 (0-1); 0 = power saving mode when temperature sensor is
            // not in use
            tempS2 = 0;                 // temperature sensor S2 (0-1); 0 = power saving mode when temperature sensor is
            // not in use
            tempEn = 0;                 // temperature sensor enable (0 = disable; 1 = enable)
            SetDigOutHiZ();             // auxiliary digital output state

            weakMiso = 1;               // weak MISO (0 = MISO line is HiZ when CS is inactive; 1 = MISO line is weakly
            // driven when CS is inactive)
            twosComp = 0;               // two's complement ADC results (0 = unsigned offset representation; 1 = signed
            // representation)
            absMode = 0;                // absolute value mode (0 = normal output; 1 = output passed through abs(x) function)
            EnableDsp(true);            // DSP offset removal enable/disable
            SetDspCutoffFreq(1.0);      // DSP offset removal HPF cutoff freqeuncy

            zcheckDacPower = 0;         // impedance testing DAC power-up (0 = power down; 1 = power up)
            zcheckLoad = 0;             // impedance testing dummy load (0 = normal operation; 1 = insert 60 pF to ground)
            SetZcheckScale(ZcheckCs.ZcheckCs100fF);  // impedance testing scale factor (100 fF, 1.0 pF, or 10.0 pF)
            zcheckConnAll = 0;          // impedance testing connect all (0 = normal operation; 1 = connect all electrodes together)
            SetZcheckPolarity(ZcheckPolarity.ZcheckPositiveInput); // impedance testing polarity select (RHD2216 only) (0 = test positive inputs;
            // 1 = test negative inputs)
            EnableZcheck(false);        // impedance testing enable/disable

            SetZcheckChannel(0);        // impedance testing amplifier select (0-63, but MSB is ignored, so 0-31 in practice)

            offChipRH1 = 0;             // bandwidth resistor RH1 on/off chip (0 = on chip; 1 = off chip)
            offChipRH2 = 0;             // bandwidth resistor RH2 on/off chip (0 = on chip; 1 = off chip)
            offChipRL = 0;              // bandwidth resistor RL on/off chip (0 = on chip; 1 = off chip)
            adcAux1En = 1;              // enable ADC aux1 input (when RH1 is on chip) (0 = disable; 1 = enable)
            adcAux2En = 1;              // enable ADC aux2 input (when RH2 is on chip) (0 = disable; 1 = enable)
            adcAux3En = 1;              // enable ADC aux3 input (when RL is on chip) (0 = disable; 1 = enable)

            SetUpperBandwidth(10000.0); // set upper bandwidth of amplifiers
            SetLowerBandwidth(1.0);     // set lower bandwidth of amplifiers

            PowerUpAllAmps();           // turn on all amplifiers
        }

        // Define RHD2000 per-channel sampling rate so that certain sampling-rate-dependent registers are set correctly
        // (This function does not change the sampling rate of the FPGA; for this, use Rhd2000EvalBoard::setSampleRate.)
        public void DefineSampleRate(double newSampleRate)
        {
            sampleRate = newSampleRate;

            muxLoad = 0;

            if (sampleRate < 3334.0)
            {
                muxBias = 40;
                adcBufferBias = 32;
            }
            else if (sampleRate < 4001.0)
            {
                muxBias = 40;
                adcBufferBias = 16;
            }
            else if (sampleRate < 5001.0)
            {
                muxBias = 40;
                adcBufferBias = 8;
            }
            else if (sampleRate < 6251.0)
            {
                muxBias = 32;
                adcBufferBias = 8;
            }
            else if (sampleRate < 8001.0)
            {
                muxBias = 26;
                adcBufferBias = 8;
            }
            else if (sampleRate < 10001.0)
            {
                muxBias = 18;
                adcBufferBias = 4;
            }
            else if (sampleRate < 12501.0)
            {
                muxBias = 16;
                adcBufferBias = 3;
            }
            else if (sampleRate < 15001.0)
            {
                muxBias = 7;
                adcBufferBias = 3;
            }
            else
            {
                muxBias = 4;
                adcBufferBias = 2;
            }
        }

        // Enable or disable amplifier fast settle function; drive amplifiers to baseline
        // if enabled.
        public void SetFastSettle(bool enabled)
        {
            ampFastSettle = (enabled ? 1 : 0);
        }

        // Drive auxiliary digital output low
        public void SetDigOutLow()
        {
            digOut = 0;
            digOutHiZ = 0;
        }

        // Drive auxiliary digital output high
        public void SetDigOutHigh()
        {
            digOut = 1;
            digOutHiZ = 0;
        }

        // Set auxiliary digital output to high-impedance (HiZ) state
        public void SetDigOutHiZ()
        {
            digOut = 0;
            digOutHiZ = 1;
        }

        // Enable or disable ADC auxiliary input 1
        public void EnableAux1(bool enabled)
        {
            adcAux1En = (enabled ? 1 : 0);
        }

        // Enable or disable ADC auxiliary input 2
        public void EnableAux2(bool enabled)
        {
            adcAux2En = (enabled ? 1 : 0);
        }

        // Enable or disable ADC auxiliary input 3
        public void EnableAux3(bool enabled)
        {
            adcAux3En = (enabled ? 1 : 0);
        }

        // Enable or disable DSP offset removal filter
        public void EnableDsp(bool enabled)
        {
            dspEn = (enabled ? 1 : 0);
        }

        // Set the DSP offset removal filter cutoff frequency as closely to the requested
        // newDspCutoffFreq (in Hz) as possible; returns the actual cutoff frequency (in Hz).
        public double SetDspCutoffFreq(double newDspCutoffFreq)
        {
            int n;
            double x, logNewDspCutoffFreq, minLogDiff;
            double[] fCutoff = new double[16];
            double[] logFCutoff = new double[16];

            fCutoff[0] = 0.0;   // We will not be using fCutoff[0], but we initialize it to be safe

            logNewDspCutoffFreq = Math.Log10(newDspCutoffFreq);

            // Generate table of all possible DSP cutoff frequencies
            for (n = 1; n < 16; ++n)
            {
                x = Math.Pow(2.0, (double)n);
                fCutoff[n] = sampleRate * Math.Log(x / (x - 1.0)) / (2 * Math.PI);
                logFCutoff[n] = Math.Log10(fCutoff[n]);
                // cout << "  fCutoff[" << n << "] = " << fCutoff[n] << " Hz" << endl;
            }

            // Now find the closest value to the requested cutoff frequency (on a logarithmic scale)
            if (newDspCutoffFreq > fCutoff[1])
            {
                dspCutoffFreq = 1;
            }
            else if (newDspCutoffFreq < fCutoff[15])
            {
                dspCutoffFreq = 15;
            }
            else
            {
                minLogDiff = 10000000.0;
                for (n = 1; n < 16; ++n)
                {
                    if (Math.Abs(logNewDspCutoffFreq - logFCutoff[n]) < minLogDiff)
                    {
                        minLogDiff = Math.Abs(logNewDspCutoffFreq - logFCutoff[n]);
                        dspCutoffFreq = n;
                    }
                }
            }

            return fCutoff[dspCutoffFreq];
        }

        // Returns the current value of the DSP offset removal cutoff frequency (in Hz).
        public double GetDspCutoffFreq()
        {
            double x;
            x = Math.Pow(2.0, (double)dspCutoffFreq);

            return sampleRate * Math.Log(x / (x - 1.0)) / (2 * Math.PI);
        }

        // Enable or disable impedance checking mode
        public void EnableZcheck(bool enabled)
        {
            zcheckEn = (enabled ? 1 : 0);
        }

        // Power up or down impedance checking DAC
        public void SetZcheckDacPower(bool enabled)
        {
            zcheckDacPower = (enabled ? 1 : 0);
        }

        // Select the series capacitor used to convert the voltage waveform generated by the on-chip
        // DAC into an AC current waveform that stimulates a selected electrode for impedance testing
        // (ZcheckCs100fF, ZcheckCs1pF, or Zcheck10pF).
        public void SetZcheckScale(ZcheckCs scale)
        {
            switch (scale)
            {
                case ZcheckCs.ZcheckCs100fF:
                    zcheckScale = 0x00;     // Cs = 0.1 pF
                    break;
                case ZcheckCs.ZcheckCs1pF:
                    zcheckScale = 0x01;     // Cs = 1.0 pF
                    break;
                case ZcheckCs.ZcheckCs10pF:
                    zcheckScale = 0x03;     // Cs = 10.0 pF
                    break;
            }
        }

        // Select impedance testing of positive or negative amplifier inputs (RHD2216 only), based
        // on the variable polarity (ZcheckPositiveInput or ZcheckNegativeInput)
        public void SetZcheckPolarity(ZcheckPolarity polarity)
        {
            switch (polarity)
            {
                case ZcheckPolarity.ZcheckPositiveInput:
                    zcheckSelPol = 0;
                    break;
                case ZcheckPolarity.ZcheckNegativeInput:
                    zcheckSelPol = 1;
                    break;
            }
        }

        // Select the amplifier channel (0-31) for impedance testing.
        public int SetZcheckChannel(int channel)
        {
            if (channel < 0 || channel > 31)
            {
                return -1;
            }
            else
            {
                zcheckSelect = channel;
                return zcheckSelect;
            }

        }

        // Power up or down selected amplifier on chip
        public void SetAmpPowered(int channel, bool powered)
        {
            if (channel >= 0 && channel <= 31)
            {
                aPwr[channel] = (powered ? 1 : 0);
            }
        }

        // Power up all amplifiers on chip
        public void PowerUpAllAmps()
        {
            for (int channel = 0; channel < aPwr.Length; ++channel)
            {
                aPwr[channel] = 1;
            }
        }

        // Power down all amplifiers on chip
        public void PowerDownAllAmps()
        {
            for (int channel = 0; channel < aPwr.Length; ++channel)
            {
                aPwr[channel] = 0;
            }
        }

        // Returns the value of a selected RAM register (0-17) on the RHD2000 chip, based
        // on the current register variables in the class instance.
        public int GetRegisterValue(int reg)
        {
            int regout;
            const int zcheckDac = 128;  // midrange

            switch (reg)
            {
                case 0:
                    regout = (adcReferenceBw << 6) + (ampFastSettle << 5) + (ampVrefEnable << 4) +
                            (adcComparatorBias << 2) + adcComparatorSelect;
                    break;
                case 1:
                    regout = (vddSenseEnable << 6) + adcBufferBias;
                    break;
                case 2:
                    regout = muxBias;
                    break;
                case 3:
                    regout = (muxLoad << 5) + (tempS2 << 4) + (tempS1 << 3) + (tempEn << 2) +
                            (digOutHiZ << 1) + digOut;
                    break;
                case 4:
                    regout = (weakMiso << 7) + (twosComp << 6) + (absMode << 5) + (dspEn << 4) +
                            dspCutoffFreq;
                    break;
                case 5:
                    regout = (zcheckDacPower << 6) + (zcheckLoad << 5) + (zcheckScale << 3) +
                            (zcheckConnAll << 2) + (zcheckSelPol << 1) + zcheckEn;
                    break;
                case 6:
                    regout = zcheckDac;
                    break;
                case 7:
                    regout = zcheckSelect;
                    break;
                case 8:
                    regout = (offChipRH1 << 7) + rH1Dac1;
                    break;
                case 9:
                    regout = (adcAux1En << 7) + rH1Dac2;
                    break;
                case 10:
                    regout = (offChipRH2 << 7) + rH2Dac1;
                    break;
                case 11:
                    regout = (adcAux2En << 7) + rH2Dac2;
                    break;
                case 12:
                    regout = (offChipRL << 7) + rLDac1;
                    break;
                case 13:
                    regout = (adcAux3En << 7) + (rLDac3 << 6) + rLDac2;
                    break;
                case 14:
                    regout = (aPwr[7] << 7) + (aPwr[6] << 6) + (aPwr[5] << 5) + (aPwr[4] << 4) +
                            (aPwr[3] << 3) + (aPwr[2] << 2) + (aPwr[1] << 1) + aPwr[0];
                    break;
                case 15:
                    regout = (aPwr[15] << 7) + (aPwr[14] << 6) + (aPwr[13] << 5) + (aPwr[12] << 4) +
                            (aPwr[11] << 3) + (aPwr[10] << 2) + (aPwr[9] << 1) + aPwr[0];
                    break;
                case 16:
                    regout = (aPwr[23] << 7) + (aPwr[22] << 6) + (aPwr[21] << 5) + (aPwr[20] << 4) +
                            (aPwr[19] << 3) + (aPwr[18] << 2) + (aPwr[17] << 1) + aPwr[16];
                    break;
                case 17:
                    regout = (aPwr[31] << 7) + (aPwr[30] << 6) + (aPwr[29] << 5) + (aPwr[28] << 4) +
                            (aPwr[27] << 3) + (aPwr[26] << 2) + (aPwr[25] << 1) + aPwr[24];
                    break;
                default:
                    regout = -1;
                    break;
            }
            return regout;
        }

        // Sets the on-chip RH1 and RH2 DAC values appropriately to set a particular amplifier
        // upper bandwidth (in Hz).  Returns an estimate of the actual upper bandwidth achieved.
        public double SetUpperBandwidth(double upperBandwidth)
        {
            const double RH1Base = 2200.0;
            const double RH1Dac1Unit = 600.0;
            const double RH1Dac2Unit = 29400.0;
            const int RH1Dac1Steps = 63;
            const int RH1Dac2Steps = 31;

            const double RH2Base = 8700.0;
            const double RH2Dac1Unit = 763.0;
            const double RH2Dac2Unit = 38400.0;
            const int RH2Dac1Steps = 63;
            const int RH2Dac2Steps = 31;

            double actualUpperBandwidth;
            double rH1Target, rH2Target;
            double rH1Actual, rH2Actual;
            int i;

            // Upper bandwidths higher than 30 kHz don't work well with the RHD2000 amplifiers
            if (upperBandwidth > 30000.0)
            {
                upperBandwidth = 30000.0;
            }

            rH1Target = RH1FromUpperBandwidth(upperBandwidth);

            rH1Dac1 = 0;
            rH1Dac2 = 0;
            rH1Actual = RH1Base;

            for (i = 0; i < RH1Dac2Steps; ++i)
            {
                if (rH1Actual < rH1Target - (RH1Dac2Unit - RH1Dac1Unit / 2))
                {
                    rH1Actual += RH1Dac2Unit;
                    ++rH1Dac2;
                }
            }

            for (i = 0; i < RH1Dac1Steps; ++i)
            {
                if (rH1Actual < rH1Target - (RH1Dac1Unit / 2))
                {
                    rH1Actual += RH1Dac1Unit;
                    ++rH1Dac1;
                }
            }

            rH2Target = RH2FromUpperBandwidth(upperBandwidth);

            rH2Dac1 = 0;
            rH2Dac2 = 0;
            rH2Actual = RH2Base;

            for (i = 0; i < RH2Dac2Steps; ++i)
            {
                if (rH2Actual < rH2Target - (RH2Dac2Unit - RH2Dac1Unit / 2))
                {
                    rH2Actual += RH2Dac2Unit;
                    ++rH2Dac2;
                }
            }

            for (i = 0; i < RH2Dac1Steps; ++i)
            {
                if (rH2Actual < rH2Target - (RH2Dac1Unit / 2))
                {
                    rH2Actual += RH2Dac1Unit;
                    ++rH2Dac1;
                }
            }

            double actualUpperBandwidth1, actualUpperBandwidth2;

            actualUpperBandwidth1 = UpperBandwidthFromRH1(rH1Actual);
            actualUpperBandwidth2 = UpperBandwidthFromRH2(rH2Actual);

            // Upper bandwidth estimates calculated from actual RH1 value and acutal RH2 value
            // should be very close; we will take their geometric mean to get a single
            // number.
            actualUpperBandwidth = Math.Sqrt(actualUpperBandwidth1 * actualUpperBandwidth2);

            /*
            cout << endl;
            cout << "Rhd2000Registers::setUpperBandwidth" << endl;
            cout << fixed << setprecision(1);

            cout << "RH1 DAC2 = " << rH1Dac2 << ", DAC1 = " << rH1Dac1 << endl;
            cout << "RH1 target: " << rH1Target << " Ohms" << endl;
            cout << "RH1 actual: " << rH1Actual << " Ohms" << endl;

            cout << "RH2 DAC2 = " << rH2Dac2 << ", DAC1 = " << rH2Dac1 << endl;
            cout << "RH2 target: " << rH2Target << " Ohms" << endl;
            cout << "RH2 actual: " << rH2Actual << " Ohms" << endl;

            cout << "Upper bandwidth target: " << upperBandwidth << " Hz" << endl;
            cout << "Upper bandwidth actual: " << actualUpperBandwidth << " Hz" << endl;

            cout << endl;
            cout << setprecision(6);
            cout.unsetf(ios::floatfield);
            */

            return actualUpperBandwidth;
        }

        // Sets the on-chip RL DAC values appropriately to set a particular amplifier
        // lower bandwidth (in Hz).  Returns an estimate of the actual lower bandwidth achieved.
        public double SetLowerBandwidth(double lowerBandwidth)
        {
            const double RLBase = 3500.0;
            const double RLDac1Unit = 175.0;
            const double RLDac2Unit = 12700.0;
            const double RLDac3Unit = 3000000.0;
            const int RLDac1Steps = 127;
            const int RLDac2Steps = 63;

            double actualLowerBandwidth;
            double rLTarget;
            double rLActual;
            int i;

            // Lower bandwidths higher than 1.5 kHz don't work well with the RHD2000 amplifiers
            if (lowerBandwidth > 1500.0)
            {
                lowerBandwidth = 1500.0;
            }

            rLTarget = RLFromLowerBandwidth(lowerBandwidth);

            rLDac1 = 0;
            rLDac2 = 0;
            rLDac3 = 0;
            rLActual = RLBase;

            if (lowerBandwidth < 0.15)
            {
                rLActual += RLDac3Unit;
                ++rLDac3;
            }

            for (i = 0; i < RLDac2Steps; ++i)
            {
                if (rLActual < rLTarget - (RLDac2Unit - RLDac1Unit / 2))
                {
                    rLActual += RLDac2Unit;
                    ++rLDac2;
                }
            }

            for (i = 0; i < RLDac1Steps; ++i)
            {
                if (rLActual < rLTarget - (RLDac1Unit / 2))
                {
                    rLActual += RLDac1Unit;
                    ++rLDac1;
                }
            }

            actualLowerBandwidth = LowerBandwidthFromRL(rLActual);

            /*
            cout << endl;
            cout << fixed << setprecision(1);
            cout << "Rhd2000Registers::setLowerBandwidth" << endl;

            cout << "RL DAC3 = " << rLDac3 << ", DAC2 = " << rLDac2 << ", DAC1 = " << rLDac1 << endl;
            cout << "RL target: " << rLTarget << " Ohms" << endl;
            cout << "RL actual: " << rLActual << " Ohms" << endl;

            cout << setprecision(3);

            cout << "Lower bandwidth target: " << lowerBandwidth << " Hz" << endl;
            cout << "Lower bandwidth actual: " << actualLowerBandwidth << " Hz" << endl;

            cout << endl;
            cout << setprecision(6);
            cout.unsetf(ios::floatfield);
            */

            return actualLowerBandwidth;
        }

        // Create a list of 60 commands to program most RAM registers on a RHD2000 chip, read those values
        // back to confirm programming, read ROM registers, and (if calibrate == true) run ADC calibration.
        // Returns the length of the command list.
        public int CreateCommandListRegisterConfig(List<int> commandList, bool calibrate)
        {
            commandList.Clear();    // if command list already exists, erase it and start a new one

            // Start with a few dummy commands in case chip is still powering up
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 63));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 63));

            // Program RAM registers
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegWrite, 0, GetRegisterValue(0)));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegWrite, 1, GetRegisterValue(1)));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegWrite, 2, GetRegisterValue(2)));
            // Don't program Register 3 (MUX Load, Temperature Sensor, and Auxiliary Digital Output);
            // control temperature sensor in another command stream
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegWrite, 4, GetRegisterValue(4)));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegWrite, 5, GetRegisterValue(5)));
            // Don't program Register 6 (Impedance Check DAC) here; create DAC waveform in another command stream
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegWrite, 7, GetRegisterValue(7)));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegWrite, 8, GetRegisterValue(8)));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegWrite, 9, GetRegisterValue(9)));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegWrite, 10, GetRegisterValue(10)));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegWrite, 11, GetRegisterValue(11)));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegWrite, 12, GetRegisterValue(12)));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegWrite, 13, GetRegisterValue(13)));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegWrite, 14, GetRegisterValue(14)));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegWrite, 15, GetRegisterValue(15)));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegWrite, 16, GetRegisterValue(16)));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegWrite, 17, GetRegisterValue(17)));

            // Read ROM registers
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 63));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 62));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 61));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 60));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 59));

            // Read chip name from ROM
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 48));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 49));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 50));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 51));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 52));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 53));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 54));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 55));

            // Read Intan name from ROM
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 40));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 41));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 42));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 43));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 44));

            // Read back RAM registers to confirm programming
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 0));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 1));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 2));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 3));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 4));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 5));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 6));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 7));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 8));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 9));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 10));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 11));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 12));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 13));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 14));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 15));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 16));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 17));

            // Optionally, run ADC calibration (should only be run once after board is plugged in)
            if (calibrate)
            {
                commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandCalibrate));
            }
            else
            {
                commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 63));
            }

            // End with a few dummy commands
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 63));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 63));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 63));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 63));
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 63));

            return commandList.Count;
        }

        // Create a list of 60 commands to sample auxiliary ADC inputs, temperature sensor, and supply
        // voltage sensor.  One temperature reading (one sample of ResultA and one sample of ResultB)
        // is taken during this 60-command sequence.  One supply voltage sample is taken.  Auxiliary
        // ADC inputs are continuously sampled at 1/4 the amplifier sampling rate.
        //
        // Since this command list consists of writing to Register 3, it also sets the state of the
        // auxiliary digital output.  If the digital output value needs to be changed dynamically,
        // then variations of this command list need to be generated for each state and programmed into
        // different RAM banks, and the appropriate command list selected at the right time.
        //
        // Returns the length of the command list.
        public int CreateCommandListTempSensor(List<int> commandList)
        {
            int i;

            commandList.Clear();    // if command list already exists, erase it and start a new one

            tempEn = 1;

            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 32));     // sample AuxIn1
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 33));     // sample AuxIn2
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 34));     // sample AuxIn3
            tempS1 = tempEn;
            tempS2 = 0;
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegWrite, 3, GetRegisterValue(3)));

            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 32));     // sample AuxIn1
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 33));     // sample AuxIn2
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 34));     // sample AuxIn3
            tempS1 = tempEn;
            tempS2 = tempEn;
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegWrite, 3, GetRegisterValue(3)));

            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 32));     // sample AuxIn1
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 33));     // sample AuxIn2
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 34));     // sample AuxIn3
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 49));     // sample Temperature Sensor

            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 32));     // sample AuxIn1
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 33));     // sample AuxIn2
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 34));     // sample AuxIn3
            tempS1 = 0;
            tempS2 = tempEn;
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegWrite, 3, GetRegisterValue(3)));

            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 32));     // sample AuxIn1
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 33));     // sample AuxIn2
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 34));     // sample AuxIn3
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 49));     // sample Temperature Sensor

            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 32));     // sample AuxIn1
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 33));     // sample AuxIn2
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 34));     // sample AuxIn3
            tempS1 = 0;
            tempS2 = 0;
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegWrite, 3, GetRegisterValue(3)));

            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 32));     // sample AuxIn1
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 33));     // sample AuxIn2
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 34));     // sample AuxIn3
            commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 48));     // sample Supply Voltage Sensor

            for (i = 0; i < 8; ++i)
            {
                commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 32));     // sample AuxIn1
                commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 33));     // sample AuxIn2
                commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandConvert, 34));     // sample AuxIn3
                commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegRead, 63));      // dummy command
            }

            return commandList.Count;
        }

        // Create a list of up to 1024 commands to generate a sine wave of particular frequency (in Hz) and
        // amplitude (in DAC steps, 0-128) using the on-chip impedance testing voltage DAC.  If frequency is set to zero,
        // a DC baseline waveform is created.
        // Returns the length of the command list.
        public int CreateCommandListZcheckDac(List<int> commandList, double frequency, double amplitude)
        {
            int i, period, value;
            double t;

            commandList.Clear();    // if command list already exists, erase it and start a new one

            if (amplitude < 0.0 || amplitude > 128.0)
            {
                throw new ArgumentException("Amplitude out of range.", "amplitude");
            }
            if (frequency < 0.0)
            {
                throw new ArgumentException("Negative frequency not allowed.", "frequency");
            }
            else if (frequency > sampleRate / 4.0)
            {
                throw new ArgumentException("Frequency too high relative to sampling rate.", "frequency");
            }
            if (frequency == 0.0)
            {
                for (i = 0; i < MaxCommandLength; ++i)
                {
                    commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegWrite, 6, 128));
                }
            }
            else
            {
                period = (int)Math.Floor(sampleRate / frequency + 0.5);
                if (period > MaxCommandLength)
                {
                    throw new ArgumentException("Frequency too low.", "frequency");
                }
                else
                {
                    t = 0.0;
                    for (i = 0; i < period; ++i)
                    {
                        value = (int)Math.Floor(amplitude * Math.Sin(2 * Math.PI * frequency * t) + 128.0 + 0.5);
                        if (value < 0)
                        {
                            value = 0;
                        }
                        else if (value > 255)
                        {
                            value = 255;
                        }
                        commandList.Add(CreateRhd2000Command(Rhd2000CommandType.Rhd2000CommandRegWrite, 6, value));
                        t += 1.0 / sampleRate;
                    }
                }
            }

            return commandList.Count;
        }

        // Return a 16-bit MOSI command (CALIBRATE or CLEAR)
        public int CreateRhd2000Command(Rhd2000CommandType commandType)
        {
            switch (commandType)
            {
                case Rhd2000CommandType.Rhd2000CommandCalibrate:
                    return 0x5500;   // 0101010100000000
                case Rhd2000CommandType.Rhd2000CommandCalClear:
                    return 0x6a00;   // 0110101000000000
                default:
                    throw new ArgumentException("Only 'Calibrate' or 'Clear Calibration' commands take zero arguments.", "commandType");
            }
        }

        // Return a 16-bit MOSI command (CONVERT or READ)
        public int CreateRhd2000Command(Rhd2000CommandType commandType, int arg1)
        {
            switch (commandType)
            {
                case Rhd2000CommandType.Rhd2000CommandConvert:
                    if (arg1 < 0 || arg1 > 63)
                    {
                        throw new ArgumentException("Channel number out of range.", "arg1");
                    }
                    return 0x0000 + (arg1 << 8);  // 00cccccc0000000h; if the command is 'Convert',
                // arg1 is the channel number
                case Rhd2000CommandType.Rhd2000CommandRegRead:
                    if (arg1 < 0 || arg1 > 63)
                    {
                        throw new ArgumentException("Register address out of range.", "arg1");
                    }
                    return 0xc000 + (arg1 << 8);  // 11rrrrrr00000000; if the command is 'Register Read',
                    // arg1 is the register address
                default:
                    throw new ArgumentException("Only 'Convert' and 'Register Read' commands take one argument.", "commandType");
            }
        }

        // Return a 16-bit MOSI command (WRITE)
        public int CreateRhd2000Command(Rhd2000CommandType commandType, int arg1, int arg2)
        {
            switch (commandType)
            {
                case Rhd2000CommandType.Rhd2000CommandRegWrite:
                    if (arg1 < 0 || arg1 > 63)
                    {
                        throw new ArgumentException("Register address out of range.", "arg1");
                    }
                    if (arg2 < 0 || arg2 > 255)
                    {
                        throw new ArgumentException("Register data out of range.", "arg2");
                    }
                    return 0x8000 + (arg1 << 8) + arg2; // 10rrrrrrdddddddd; if the command is 'Register Write',
                    // arg1 is the register address and arg2 is the data
                default:
                    throw new ArgumentException("Only 'Register Write' commands take two arguments.", "commandType");
            }
        }

        // Returns the value of the RH1 resistor (in ohms) corresponding to a particular upper
        // bandwidth value (in Hz).
        double RH1FromUpperBandwidth(double upperBandwidth)
        {
            double log10f = Math.Log10(upperBandwidth);

            return 0.9730 * Math.Pow(10.0, (8.0968 - 1.1892 * log10f + 0.04767 * log10f * log10f));
        }

        // Returns the value of the RH2 resistor (in ohms) corresponding to a particular upper
        // bandwidth value (in Hz).
        double RH2FromUpperBandwidth(double upperBandwidth)
        {
            double log10f = Math.Log10(upperBandwidth);

            return 1.0191 * Math.Pow(10.0, (8.1009 - 1.0821 * log10f + 0.03383 * log10f * log10f));
        }

        // Returns the value of the RL resistor (in ohms) corresponding to a particular lower
        // bandwidth value (in Hz).
        double RLFromLowerBandwidth(double lowerBandwidth)
        {
            double log10f = Math.Log10(lowerBandwidth);

            if (lowerBandwidth < 4.0)
            {
                return 1.0061 * Math.Pow(10.0, (4.9391 - 1.2088 * log10f + 0.5698 * log10f * log10f +
                                           0.1442 * log10f * log10f * log10f));
            }
            else
            {
                return 1.0061 * Math.Pow(10.0, (4.7351 - 0.5916 * log10f + 0.08482 * log10f * log10f));
            }
        }

        // Returns the amplifier upper bandwidth (in Hz) corresponding to a particular value
        // of the resistor RH1 (in ohms).
        double UpperBandwidthFromRH1(double rH1)
        {
            double a, b, c;

            a = 0.04767;
            b = -1.1892;
            c = 8.0968 - Math.Log10(rH1 / 0.9730);

            return Math.Pow(10.0, ((-b - Math.Sqrt(b * b - 4 * a * c)) / (2 * a)));
        }

        // Returns the amplifier upper bandwidth (in Hz) corresponding to a particular value
        // of the resistor RH2 (in ohms).
        double UpperBandwidthFromRH2(double rH2)
        {
            double a, b, c;

            a = 0.03383;
            b = -1.0821;
            c = 8.1009 - Math.Log10(rH2 / 1.0191);

            return Math.Pow(10.0, ((-b - Math.Sqrt(b * b - 4 * a * c)) / (2 * a)));
        }

        // Returns the amplifier lower bandwidth (in Hz) corresponding to a particular value
        // of the resistor RL (in ohms).
        double LowerBandwidthFromRL(double rL)
        {
            double a, b, c;

            // Quadratic fit below is invalid for values of RL less than 5.1 kOhm
            if (rL < 5100.0)
            {
                rL = 5100.0;
            }

            if (rL < 30000.0)
            {
                a = 0.08482;
                b = -0.5916;
                c = 4.7351 - Math.Log10(rL / 1.0061);
            }
            else
            {
                a = 0.3303;
                b = -1.2100;
                c = 4.9873 - Math.Log10(rL / 1.0061);
            }

            return Math.Pow(10.0, ((-b - Math.Sqrt(b * b - 4 * a * c)) / (2 * a)));

        }
    }
}
