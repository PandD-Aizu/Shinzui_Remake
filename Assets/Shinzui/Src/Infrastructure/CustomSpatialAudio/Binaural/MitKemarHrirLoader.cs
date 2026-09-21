using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;

namespace Shinzui.Infrastructure.CustomSpatialAudio.Binaural
{
    /// <summary>Strict bounded reader for the official compact.zip (44.1 kHz, 128 stereo PCM16 frames).</summary>
    internal static class MitKemarHrirLoader
    {
        internal static HrirDataset Load(Stream zipStream, int outputSampleRate)
        {
            if (zipStream == null || !zipStream.CanRead || !zipStream.CanSeek)
                throw new ArgumentException("A readable, seekable ZIP stream is required.", nameof(zipStream));
            if (zipStream.Length - zipStream.Position > 16 * 1024 * 1024)
                throw new InvalidDataException("HRIR archive exceeds the 16 MiB limit.");
            if (outputSampleRate < 16000 || outputSampleRate > 96000)
                throw new ArgumentOutOfRangeException(nameof(outputSampleRate));
            var measurements = new List<HrirMeasurement>();
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, true))
            {
                if (archive.Entries.Count > 1000) throw new InvalidDataException("Too many archive entries.");
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string name = entry.Name;
                    if (!name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) continue;
                    int elevationMarker = name.IndexOf('e');
                    int azimuthMarker = name.IndexOf('a', Math.Max(0, elevationMarker + 1));
                    if (!name.StartsWith("H", StringComparison.Ordinal) || elevationMarker < 2 ||
                        azimuthMarker != name.Length - 5 ||
                        !int.TryParse(name.Substring(1, elevationMarker - 1), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out int elevation) ||
                        !int.TryParse(name.Substring(elevationMarker + 1, azimuthMarker - elevationMarker - 1),
                            NumberStyles.None, CultureInfo.InvariantCulture, out int azimuth) ||
                        elevation < -40 || elevation > 90 || azimuth < 0 || azimuth > 180)
                        throw new InvalidDataException("Unexpected MIT compact WAV filename: " + name);
                    if (entry.Length < 44 || entry.Length > 65536)
                        throw new InvalidDataException("Invalid HRIR WAV length.");
                    var bytes = new byte[(int)entry.Length];
                    using (Stream input = entry.Open())
                    {
                        int offset = 0;
                        while (offset < bytes.Length)
                        {
                            int read = input.Read(bytes, offset, bytes.Length - offset);
                            if (read == 0) throw new InvalidDataException("Truncated HRIR WAV.");
                            offset += read;
                        }
                    }
                    HrirFilter filter = HrirWavReader.ReadPcm16Stereo(bytes);
                    if (filter.SampleRate != 44100 || filter.TapCount != 128)
                        throw new InvalidDataException("MIT compact HRIRs must be 128 frames at 44100 Hz.");
                    filter = filter.Resample(outputSampleRate);
                    measurements.Add(new HrirMeasurement(azimuth, elevation, filter));
                    // The archive contains the right hemisphere from the symmetric small-pinna
                    // dataset. Its missing hemisphere uses the same measured pair with ears swapped.
                    if (azimuth > 0 && azimuth < 180)
                        measurements.Add(new HrirMeasurement(360 - azimuth, elevation, filter.MirrorEars()));
                }
            }
            if (measurements.Count == 0) throw new InvalidDataException("No MIT compact WAV measurements found.");
            return new HrirDataset(measurements);
        }
    }

    /// <summary>
    /// Control-thread RIFF/WAVE reader for custom paired HRIR data. Only uncompressed, little-endian,
    /// stereo PCM16 with 1..512 frames and 16..96 kHz is accepted; compressed/float WAV is rejected.
    /// </summary>
    public static class HrirWavReader
    {
        public static HrirFilter ReadPcm16Stereo(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 44 || bytes.Length > 65536)
                throw new InvalidDataException("HRIR WAV must contain 44..65536 bytes.");
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadUInt32() != 0x46464952) throw new InvalidDataException("Expected RIFF header.");
                uint riffSize = reader.ReadUInt32();
                if ((long)riffSize + 8 != bytes.Length || reader.ReadUInt32() != 0x45564157)
                    throw new InvalidDataException("Invalid WAVE header or RIFF length.");
                int sampleRate = 0, dataOffset = -1, dataLength = 0;
                bool foundFormat = false;
                while (stream.Position < stream.Length)
                {
                    if (stream.Length - stream.Position < 8) throw new InvalidDataException("Truncated WAV chunk header.");
                    uint chunk = reader.ReadUInt32();
                    uint length = reader.ReadUInt32();
                    long end = stream.Position + length;
                    long paddedEnd = end + (length & 1);
                    if (paddedEnd > stream.Length) throw new InvalidDataException("Truncated WAV chunk.");
                    if (chunk == 0x20746d66)
                    {
                        if (foundFormat || length < 16) throw new InvalidDataException("Invalid WAV format chunk.");
                        ushort format = reader.ReadUInt16(), channels = reader.ReadUInt16();
                        uint rate = reader.ReadUInt32(), byteRate = reader.ReadUInt32();
                        ushort alignment = reader.ReadUInt16(), bits = reader.ReadUInt16();
                        if (format != 1 || channels != 2 || bits != 16 || alignment != 4 ||
                            rate < 16000 || rate > 96000 || byteRate != rate * 4)
                            throw new InvalidDataException("Expected stereo PCM16 WAV at 16000..96000 Hz.");
                        sampleRate = (int)rate;
                        foundFormat = true;
                    }
                    else if (chunk == 0x61746164)
                    {
                        if (dataOffset >= 0 || length == 0 || length % 4 != 0 || length / 4 > HrirFilter.MaximumTapCount)
                            throw new InvalidDataException("Invalid HRIR sample count.");
                        dataOffset = (int)stream.Position;
                        dataLength = (int)length;
                    }
                    stream.Position = paddedEnd;
                }
                if (!foundFormat || dataOffset < 0) throw new InvalidDataException("Missing WAV format or data chunk.");
                var left = new float[dataLength / 4];
                var right = new float[left.Length];
                stream.Position = dataOffset;
                for (int i = 0; i < left.Length; i++)
                {
                    left[i] = reader.ReadInt16() / 32768f;
                    right[i] = reader.ReadInt16() / 32768f;
                }
                return new HrirFilter(sampleRate, left, right);
            }
        }
    }
}
