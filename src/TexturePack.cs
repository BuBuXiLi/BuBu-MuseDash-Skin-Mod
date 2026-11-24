using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Bubu
{
    internal static class TexturePack
    {
        private static readonly byte[] KeyPart1 = { 0x8B, 0x3E, 0x71, 0xC4, 0x2D, 0x96, 0x5A, 0x0F, 0xE8, 0x43, 0xB7 };
        private static readonly byte[] KeyPart2 = { 0x19, 0xD2, 0x6C, 0x84, 0xF5, 0x2A, 0x67, 0xBE, 0x30, 0x9D, 0x51, 0xC8, 0x74 };
        private static readonly byte[] KeyPart3 = { 0xA6, 0x0B, 0x4E, 0xE1, 0x37, 0x8F, 0x62, 0xD9, 0x15 };

        internal static Dictionary<string, byte[]> Read(Stream pack)
        {
            using var reader = new BinaryReader(pack, Encoding.UTF8, leaveOpen: true);
            using var aes = Aes.Create();
            aes.Key = SHA256.HashData(CombineKeyParts());
            aes.IV = ReadBytes(reader, 16);
            using var decryptor = aes.CreateDecryptor();
            using var plaintext = new CryptoStream(pack, decryptor, CryptoStreamMode.Read, leaveOpen: true);
            var utf8 = new UTF8Encoding(false, true);
            using var entries = new BinaryReader(plaintext, utf8);

            if (entries.ReadUInt32() != 0x314B5042)
                throw new InvalidDataException("BPK1 文件头无效。");

            var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            uint count = entries.ReadUInt32();
            for (uint i = 0; i < count; i++)
            {
                string name = utf8.GetString(ReadBytes(entries, entries.ReadUInt16()));
                int size = checked((int)entries.ReadUInt32());
                // Keep the last entry, as 1.5.4 did when repacking replaced a name by appending it.
                result[name] = ReadBytes(entries, size);
            }
            if (plaintext.ReadByte() != -1)
                throw new InvalidDataException("BPK1 条目结束后仍有数据。");
            return result;
        }

        private static byte[] CombineKeyParts()
        {
            using var key = new MemoryStream();
            key.Write(KeyPart1);
            key.Write(KeyPart2);
            key.Write(KeyPart3);
            return key.ToArray();
        }

        private static byte[] ReadBytes(BinaryReader reader, int count)
        {
            // A corrupt length must not allocate that many bytes before checking the stream.
            using var output = new MemoryStream();
            var buffer = new byte[Math.Min(count, 81920)];
            while (count > 0)
            {
                int read = reader.Read(buffer, 0, Math.Min(count, buffer.Length));
                if (read == 0) throw new EndOfStreamException();
                output.Write(buffer, 0, read);
                count -= read;
            }
            return output.ToArray();
        }
    }
}
