﻿/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2014 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System;
using System.Text;

namespace OpenHardwareMonitor.Hardware.CPU
{
    internal enum Vendor
    {
        Unknown,
        Intel,
        AMD
    }

    internal class CPUID
    {
        public const uint CPUID_0 = 0;
        public const uint CPUID_EXT = 0x80000000;

        private readonly uint coreMaskWith;

        private readonly uint threadMaskWith;

        public CPUID(int thread)
        {
            Thread = thread;

            uint maxCpuid = 0;
            uint maxCpuidExt = 0;

            uint eax, ebx, ecx, edx;

            if (thread >= 64)
                throw new ArgumentOutOfRangeException("thread");
            var mask = 1UL << thread;

            if (Opcode.CpuidTx(CPUID_0, 0,
                out eax, out ebx, out ecx, out edx, mask))
            {
                if (eax > 0)
                    maxCpuid = eax;
                else
                    return;

                var vendorBuilder = new StringBuilder();
                AppendRegister(vendorBuilder, ebx);
                AppendRegister(vendorBuilder, edx);
                AppendRegister(vendorBuilder, ecx);
                var cpuVendor = vendorBuilder.ToString();
                switch (cpuVendor)
                {
                    case "GenuineIntel":
                        Vendor = Vendor.Intel;
                        break;
                    case "AuthenticAMD":
                        Vendor = Vendor.AMD;
                        break;
                    default:
                        Vendor = Vendor.Unknown;
                        break;
                }
                eax = ebx = ecx = edx = 0;
                if (Opcode.CpuidTx(CPUID_EXT, 0,
                    out eax, out ebx, out ecx, out edx, mask))
                {
                    if (eax > CPUID_EXT)
                        maxCpuidExt = eax - CPUID_EXT;
                    else
                        return;
                }
                else
                {
                    throw new ArgumentOutOfRangeException("thread");
                }
            }
            else
            {
                throw new ArgumentOutOfRangeException("thread");
            }

            maxCpuid = Math.Min(maxCpuid, 1024);
            maxCpuidExt = Math.Min(maxCpuidExt, 1024);

            Data = new uint[maxCpuid + 1, 4];
            for (uint i = 0; i < (maxCpuid + 1); i++)
                Opcode.CpuidTx(CPUID_0 + i, 0,
                    out Data[i, 0], out Data[i, 1],
                    out Data[i, 2], out Data[i, 3], mask);

            ExtData = new uint[maxCpuidExt + 1, 4];
            for (uint i = 0; i < (maxCpuidExt + 1); i++)
                Opcode.CpuidTx(CPUID_EXT + i, 0,
                    out ExtData[i, 0], out ExtData[i, 1],
                    out ExtData[i, 2], out ExtData[i, 3], mask);

            var nameBuilder = new StringBuilder();
            for (uint i = 2; i <= 4; i++)
            {
                if (Opcode.CpuidTx(CPUID_EXT + i, 0,
                    out eax, out ebx, out ecx, out edx, mask))
                {
                    AppendRegister(nameBuilder, eax);
                    AppendRegister(nameBuilder, ebx);
                    AppendRegister(nameBuilder, ecx);
                    AppendRegister(nameBuilder, edx);
                }
            }
            nameBuilder.Replace('\0', ' ');
            BrandString = nameBuilder.ToString().Trim();
            nameBuilder.Replace("(R)", " ");
            nameBuilder.Replace("(TM)", " ");
            nameBuilder.Replace("(tm)", "");
            nameBuilder.Replace("CPU", "");
            nameBuilder.Replace("Quad-Core Processor", "");
            nameBuilder.Replace("Six-Core Processor", "");
            nameBuilder.Replace("Eight-Core Processor", "");
            for (var i = 0; i < 10; i++) nameBuilder.Replace("  ", " ");
            Name = nameBuilder.ToString();
            if (Name.Contains("@"))
                Name = Name.Remove(Name.LastIndexOf('@'));
            Name = Name.Trim();

            Family = ((Data[1, 0] & 0x0FF00000) >> 20) +
                     ((Data[1, 0] & 0x0F00) >> 8);
            Model = ((Data[1, 0] & 0x0F0000) >> 12) +
                    ((Data[1, 0] & 0xF0) >> 4);
            Stepping = (Data[1, 0] & 0x0F);

            ApicId = (Data[1, 1] >> 24) & 0xFF;

            switch (Vendor)
            {
                case Vendor.Intel:
                    var maxCoreAndThreadIdPerPackage = (Data[1, 1] >> 16) & 0xFF;
                    uint maxCoreIdPerPackage;
                    if (maxCpuid >= 4)
                        maxCoreIdPerPackage = ((Data[4, 0] >> 26) & 0x3F) + 1;
                    else
                        maxCoreIdPerPackage = 1;
                    threadMaskWith =
                        NextLog2(maxCoreAndThreadIdPerPackage/maxCoreIdPerPackage);
                    coreMaskWith = NextLog2(maxCoreIdPerPackage);
                    break;
                case Vendor.AMD:
                    uint corePerPackage;
                    if (maxCpuidExt >= 8)
                        corePerPackage = (ExtData[8, 2] & 0xFF) + 1;
                    else
                        corePerPackage = 1;
                    threadMaskWith = 0;
                    coreMaskWith = NextLog2(corePerPackage);
                    break;
                default:
                    threadMaskWith = 0;
                    coreMaskWith = 0;
                    break;
            }

            ProcessorId = (ApicId >> (int) (coreMaskWith + threadMaskWith));
            CoreId = ((ApicId >> (int) (threadMaskWith))
                      - (ProcessorId << (int) (coreMaskWith)));
            ThreadId = ApicId
                       - (ProcessorId << (int) (coreMaskWith + threadMaskWith))
                       - (CoreId << (int) (threadMaskWith));
        }

        public string Name { get; } = "";

        public string BrandString { get; } = "";

        public int Thread { get; }

        public Vendor Vendor { get; } = Vendor.Unknown;

        public uint Family { get; }

        public uint Model { get; }

        public uint Stepping { get; }

        public uint ApicId { get; }

        public uint ProcessorId { get; }

        public uint CoreId { get; }

        public uint ThreadId { get; }

        public uint[,] Data { get; } = new uint[0, 0];

        public uint[,] ExtData { get; } = new uint[0, 0];

        private static void AppendRegister(StringBuilder b, uint value)
        {
            b.Append((char) ((value) & 0xff));
            b.Append((char) ((value >> 8) & 0xff));
            b.Append((char) ((value >> 16) & 0xff));
            b.Append((char) ((value >> 24) & 0xff));
        }

        private static uint NextLog2(long x)
        {
            if (x <= 0)
                return 0;

            x--;
            uint count = 0;
            while (x > 0)
            {
                x >>= 1;
                count++;
            }

            return count;
        }
    }
}