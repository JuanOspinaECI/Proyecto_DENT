﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Iot.Device.Ssd13xx.Samples
{
    /// <summary>
    /// 16x16 font sample
    /// </summary>
    public class DoubleByteFont : IFont
    {
        private static readonly byte[][] _fontTable =
        {
            new byte [] {
                0x00,0x00,0x00,0x00,0xC0,0x03,0x60,0x06,0x30,0x0C,0x30,0x0C,0x60,0x06,0xC0,0x03,
                0x60,0x06,0x30,0x0C,0x30,0x0C,0x30,0x0C,0x60,0x06,0xC0,0x03,0x00,0x00,0x00,0x00,
            },  // 8 [0xFF18]
            new byte [] {
                0x00,0x00,0x00,0x00,0xC0,0x03,0x60,0x06,0x30,0x0C,0x30,0x0C,0x30,0x0C,0x30,0x0C,
                0x60,0x0E,0xC0,0x0F,0x00,0x0C,0x30,0x0C,0x60,0x06,0xC0,0x03,0x00,0x00,0x00,0x00,
            },  // 9 [0xFF19]
            new byte [] {
                0x00,0x00,0x00,0x00,0x00,0x01,0x80,0x03,0x80,0x02,0x40,0x06,0x40,0x06,0x20,0x0C,
                0x20,0x0C,0xE0,0x0F,0x30,0x1C,0x10,0x18,0x10,0x18,0x38,0x3C,0x00,0x00,0x00,0x00,
            },  // A [0xFF21]
            new byte [] {
                0x00,0x00,0x00,0x00,0xF8,0x07,0x30,0x1C,0x30,0x18,0x30,0x18,0x30,0x0C,0xF0,0x0F,
                0x30,0x18,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x1C,0xF8,0x07,0x00,0x00,0x00,0x00,
            },  // B [0xFF22]            
            new byte [] {
                0x00,0x01,0x00,0x01,0x10,0x01,0x3F,0x01,0x08,0x21,0xE8,0x7F,0x08,0x21,0x08,0x21,
                0x08,0x21,0x08,0x21,0xB8,0x20,0x8F,0x20,0x82,0x20,0x40,0x22,0x20,0x14,0x10,0x08,
            },  // 功 [0x529F]
            new byte [] {
                0x80,0x00,0x80,0x00,0x80,0x10,0xFE,0x3F,0x80,0x00,0x80,0x00,0x80,0x20,0xFF,0x7F,
                0x80,0x00,0x40,0x01,0x40,0x01,0x20,0x02,0x20,0x02,0x10,0x0C,0x08,0x70,0x06,0x20,
            },  // 夫 [0x592B]
            new byte [] {
                0x00,0x00,0x00,0x00,0x00,0x09,0xC0,0x0F,0x30,0x09,0x30,0x01,0x60,0x01,0x80,0x03,
                0x00,0x0D,0x00,0x19,0x18,0x19,0x30,0x0D,0xC0,0x03,0x00,0x01,0x00,0x00,0x00,0x00,
            },  // ＄ [0xE0E0]
        };

        public override byte Width { get => 16; }
        public override byte Height { get => 16; }

        public override byte[] this[char character]
        {
            get
            {
                switch ((byte)character)
                {
                    case 24:
                        return _fontTable[0];
                    case 25:
                        return _fontTable[1];
                    case 33:
                        return _fontTable[2];
                    case 34:
                        return _fontTable[3];
                    case 159:
                        return _fontTable[4];
                    case 43:
                        return _fontTable[5];
                    default:
                        return _fontTable[6];
                }
            }
        }
    }
}
