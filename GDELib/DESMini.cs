using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDELib
{
    internal class DESMini
    {
        public void SaveMatrix(int[,] matrix, BinaryWriter writer)
        {
            MatrixIndexStorage.SaveMatrix(writer, matrix);
        }
        public int[,] ReadMatrix(BinaryReader reader)
        {
            return MatrixIndexStorage.LoadMatrix(reader);
        }
    }

    internal class ByteTrieNode
    {
        public byte Value;
        public Dictionary<byte, ByteTrieNode> Children = new Dictionary<byte, ByteTrieNode>();
        public bool IsLeaf = false;

        public ByteTrieNode() { }
        public ByteTrieNode(byte value) { Value = value; }
    }

    internal class ByteTrie
    {
        const byte META_HAS_CHILDREN = 0x80;
        const byte META_IS_LEAF = 0x40;
        const byte META_COMPRESSED = 0x20;
        const byte META_CHILDCOUNT_MASK = 0x1F; // 5 бит -> 0..31

        public ByteTrieNode Root = new ByteTrieNode(0);

        // Добавление произвольной последовательности байт (поддерживает произвольную длину)
        public void AddBytesDirect(byte[] bytes)
        {
            AddBytes(Root, bytes, 0);
        }

        private void AddBytes(ByteTrieNode node, byte[] bytes, int index)
        {
            if (index >= bytes.Length)
            {
                node.IsLeaf = true;
                return;
            }

            byte b = bytes[index];
            if (!node.Children.ContainsKey(b))
            {
                node.Children[b] = new ByteTrieNode(b);
            }

            AddBytes(node.Children[b], bytes, index + 1);
        }

        public void Print(ByteTrieNode node, string indent = "")
        {
            if (node != Root || indent != "")
                Console.WriteLine($"{indent}{node.Value:X2}" + (node.IsLeaf ? " (leaf)" : ""));

            foreach (var child in node.Children.Values.OrderBy(c => c.Value))
            {
                Print(child, indent + "  ");
            }
        }

        // УЛЬТРА-КОМПАКТНАЯ сериализация - исключаем листовые узлы без детей
        public void SaveTreeUltraCompact(BinaryWriter writer)
        {
            SaveNodeUltraCompact(writer, Root);
        }

        // Запись с bitmap флагами (каждому ребенку — 1 бит: 1 = есть поддерево, 0 = нет), с path-compression
        private void SaveNodeUltraCompact(BinaryWriter writer, ByteTrieNode node)
        {
            // 1) Вычислим label: следуем по цепочке single-child узлов, пока возможно
            List<byte> label = new List<byte>();
            ByteTrieNode last = node;
            while (last.Children.Count == 1 && !last.IsLeaf)
            {
                var onlyChild = last.Children.Values.First();
                label.Add(onlyChild.Value);
                last = onlyChild;
            }

            bool hasChildren = last.Children.Count > 0;
            // leaf относится к концу цепочки: если мы свернули цепочку, то конечный узел (last) может быть листом
            bool isLeaf = last.IsLeaf;

            int childCount = Math.Min(last.Children.Count, META_CHILDCOUNT_MASK); // 0..31
            if (last.Children.Count > META_CHILDCOUNT_MASK)
                throw new InvalidOperationException("Too many children for compact format (increase childcount bits).");

            byte meta = 0;
            if (hasChildren) meta |= META_HAS_CHILDREN;
            if (isLeaf) meta |= META_IS_LEAF;
            if (label.Count > 0) meta |= META_COMPRESSED;
            meta |= (byte)childCount;

            writer.Write(meta);

            if (label.Count > 0)
            {
                if (label.Count > 255) throw new InvalidOperationException("Label too long for one-byte length.");
                writer.Write((byte)label.Count);
                writer.Write(label.ToArray());
            }

            if (hasChildren)
            {
                // записываем значения всех детей конечного узла
                var sortedChildren = last.Children.Values.OrderBy(c => c.Value).ToList();
                for (int i = 0; i < childCount; i++)
                {
                    writer.Write(sortedChildren[i].Value);
                }

                // bitmap: 1 если у child есть поддерево (т.е. child.Children.Count > 0)
                int bits = childCount;
                int bytesNeeded = (bits + 7) / 8;
                byte[] bitmap = new byte[bytesNeeded];

                for (int i = 0; i < childCount; i++)
                {
                    var child = sortedChildren[i];
                    bool hasSubtree = child.Children.Count > 0;
                    if (hasSubtree)
                    {
                        int byteIndex = i / 8;
                        int bitIndex = i % 8;
                        bitmap[byteIndex] |= (byte)(1 << bitIndex);
                    }
                }

                writer.Write(bitmap);

                // рекурсивно сохраняем поддеревья только для тех детей, где bitmap bit == 1
                for (int i = 0; i < childCount; i++)
                {
                    var child = sortedChildren[i];
                    int byteIndex = i / 8;
                    int bitIndex = i % 8;
                    bool hasSubtree = (bitmap[byteIndex] & (1 << bitIndex)) != 0;
                    if (hasSubtree)
                    {
                        SaveNodeUltraCompact(writer, child);
                    }
                }
            }
        }

        public void LoadTreeUltraCompact(BinaryReader reader)
        {
            Root = LoadNodeUltraCompact(reader, true);
        }

        // Чтение с учетом bitmap и path-compression
        private ByteTrieNode LoadNodeUltraCompact(BinaryReader reader, bool isRoot = false)
        {
            if (reader.BaseStream.Position >= reader.BaseStream.Length)
                return null;

            byte meta = reader.ReadByte();
            bool hasChildren = (meta & META_HAS_CHILDREN) != 0;
            bool isLeaf = (meta & META_IS_LEAF) != 0;
            bool compressed = (meta & META_COMPRESSED) != 0;
            int childCount = meta & META_CHILDCOUNT_MASK;

            ByteTrieNode startNode = isRoot ? Root : new ByteTrieNode();
            // не присваиваем startNode.IsLeaf сейчас — сделаем это ниже, в зависимости от compressed

            // Если есть label — читаем его и строим цепочку узлов
            ByteTrieNode last = startNode;
            if (compressed)
            {
                int labelLen = reader.ReadByte();
                byte[] label = reader.ReadBytes(labelLen);
                for (int i = 0; i < labelLen; i++)
                {
                    var next = new ByteTrieNode(label[i]);
                    last.Children[label[i]] = next;
                    last = next;
                }
                // флаг isLeaf относится к концу цепочки
                last.IsLeaf = isLeaf;
            }
            else
            {
                // нет label — флаг относится к startNode
                startNode.IsLeaf = isLeaf;
            }

            // Теперь last — конечный узел цепочки; у него могут быть дети
            if (hasChildren && childCount > 0)
            {
                byte[] childValues = new byte[childCount];
                for (int i = 0; i < childCount; i++)
                    childValues[i] = reader.ReadByte();

                int bits = childCount;
                int bytesNeeded = (bits + 7) / 8;
                byte[] bitmap = reader.ReadBytes(bytesNeeded);
                if (bitmap.Length != bytesNeeded)
                    throw new EndOfStreamException("Unexpected end while reading bitmap.");

                // Создаём детей и при необходимости рекурсивно загружаем их поддеревья
                for (int i = 0; i < childCount; i++)
                {
                    byte val = childValues[i];
                    var childNode = new ByteTrieNode(val);
                    last.Children[val] = childNode;

                    int byteIndex = i / 8;
                    int bitIndex = i % 8;
                    bool hasSubtree = (bitmap[byteIndex] & (1 << bitIndex)) != 0;
                    if (hasSubtree)
                    {
                        var loaded = LoadNodeUltraCompact(reader);
                        if (loaded != null)
                        {
                            childNode.Children = loaded.Children;
                            childNode.IsLeaf = loaded.IsLeaf;
                        }
                    }
                    else
                    {
                        childNode.IsLeaf = true;
                    }
                }
            }

            return startNode;
        }
    }

    static internal class KeysNibbleMap
    {
        public static void SaveKeysAsNibbleMap(BinaryWriter writer, IEnumerable<int> keys, int universeSize = 256)
        {
            if (universeSize <= 0 || universeSize > 65535) throw new ArgumentOutOfRangeException(nameof(universeSize));

            bool[] present = new bool[universeSize];
            foreach (var k in keys)
            {
                if (k >= 0 && k < universeSize) present[k] = true;
            }

            writer.Write((byte)'K');
            writer.Write((ushort)universeSize);

            int nibbleCount = (universeSize + 3) / 4;
            int byteCount = (nibbleCount + 1) / 2;
            for (int b = 0; b < byteCount; b++)
            {
                int nibbleIndexLow = b * 2;
                byte low = 0;
                if (nibbleIndexLow < nibbleCount)
                {
                    int baseVal = nibbleIndexLow * 4;
                    for (int i = 0; i < 4; i++)
                    {
                        int val = baseVal + i;
                        if (val < universeSize && present[val]) low |= (byte)(1 << i);
                    }
                }

                int nibbleIndexHigh = nibbleIndexLow + 1;
                byte high = 0;
                if (nibbleIndexHigh < nibbleCount)
                {
                    int baseVal = nibbleIndexHigh * 4;
                    for (int i = 0; i < 4; i++)
                    {
                        int val = baseVal + i;
                        if (val < universeSize && present[val]) high |= (byte)(1 << i);
                    }
                }

                byte combined = (byte)(low | (high << 4));
                writer.Write(combined);
            }
        }

        public static List<int> LoadKeysNibbleMapIfPresent(BinaryReader reader)
        {
            long startPos = reader.BaseStream.Position;
            if (reader.BaseStream.Position >= reader.BaseStream.Length) return null;

            int marker = reader.ReadByte();
            if (marker != 'K')
            {
                reader.BaseStream.Position = startPos;
                return null;
            }

            if (reader.BaseStream.Position + 2 > reader.BaseStream.Length)
                throw new EndOfStreamException("Unexpected end while reading keys universe size.");

            ushort universeSize = reader.ReadUInt16();
            int nibbleCount = (universeSize + 3) / 4;
            int byteCount = (nibbleCount + 1) / 2;

            if (reader.BaseStream.Position + byteCount > reader.BaseStream.Length)
                throw new EndOfStreamException("Unexpected end while reading keys nibble map.");

            List<int> found = new List<int>();
            for (int b = 0; b < byteCount; b++)
            {
                byte combined = reader.ReadByte();
                byte low = (byte)(combined & 0x0F);
                byte high = (byte)((combined >> 4) & 0x0F);

                int nibbleIndexLow = b * 2;
                int baseLow = nibbleIndexLow * 4;
                for (int i = 0; i < 4; i++)
                {
                    int v = baseLow + i;
                    if (v >= universeSize) break;
                    if ((low & (1 << i)) != 0) found.Add(v);
                }

                int nibbleIndexHigh = nibbleIndexLow + 1;
                int baseHigh = nibbleIndexHigh * 4;
                for (int i = 0; i < 4; i++)
                {
                    int v = baseHigh + i;
                    if (v >= universeSize) break;
                    if ((high & (1 << i)) != 0) found.Add(v);
                }
            }

            return found;
        }
    }

    static internal class MatrixIndexStorage
    {
        public const int MAX_MAPPED = 14;
        public const int RESERVED_RAW_NIBBLE = 0xE;

        // Helpers: ZigZag encode/decode to handle signed ints when trimming leading zeros
        private static uint ZigZagEncode(int v) => (uint)((v << 1) ^ (v >> 31));
        private static int ZigZagDecode(uint n) => (int)((n >> 1) ^ (-(int)(n & 1)));

        // Encode a single int into a compact big-endian-with-length form:
        // [len:1 byte][len bytes big-endian trimmed-leading-zeros (len>=1 && len<=4)]
        private static byte[] EncodeIntForTrie(int value, bool useZigZag)
        {
            uint u = useZigZag ? ZigZagEncode(value) : unchecked((uint)value);
            byte[] full = BitConverter.GetBytes(u); // little-endian
            Array.Reverse(full); // big-endian
            int start = 0;
            while (start < full.Length - 1 && full[start] == 0) start++;
            int len = full.Length - start; // 1..4
            byte[] outb = new byte[1 + len];
            outb[0] = (byte)len;
            Array.Copy(full, start, outb, 1, len);
            return outb;
        }

        // Decode concatenated EncodeIntForTrie bytes -> list of ints
        private static List<int> DecodeTrieSequenceToInts(byte[] seqBytes, bool useZigZag)
        {
            var res = new List<int>();
            int pos = 0;
            while (pos < seqBytes.Length)
            {
                int len = seqBytes[pos++];
                if (len < 1 || len > 4 || pos + len > seqBytes.Length)
                    throw new InvalidDataException("Bad encoded int in trie sequence.");
                byte[] be = new byte[4];
                int pad = 4 - len;
                for (int i = 0; i < pad; i++) be[i] = 0;
                Array.Copy(seqBytes, pos, be, pad, len);
                pos += len;
                Array.Reverse(be); // now little-endian
                int v = BitConverter.ToInt32(be, 0);
                if (useZigZag)
                {
                    uint uu = unchecked((uint)v);
                    int orig = ZigZagDecode(uu);
                    res.Add(orig);
                }
                else res.Add(v);
            }
            return res;
        }

        // Build candidate sequences from matrix: horizontal sequences of neighboring cells up to maxCellsSeq.
        // Returns top sequences (by simple score) as List<int[]>
        private static List<int[]> BuildTopSequencesFromMatrix(int[,] matrix, int maxCellsSeq = 4, int maxCandidates = MAX_MAPPED, bool useZigZag = true)
        {
            var freq = new Dictionary<string, int>();
            var seqBytesStore = new Dictionary<string, byte[]>();

            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);

            Func<int, byte[]> enc = (val) => EncodeIntForTrie(val, useZigZag);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    var concat = new List<byte>();
                    for (int take = 1; take <= maxCellsSeq; take++)
                    {
                        int cc = c + take - 1;
                        if (cc >= cols) break;
                        byte[] b = enc(matrix[r, cc]);
                        concat.AddRange(b);
                        string key = Convert.ToBase64String(concat.ToArray());
                        if (!freq.ContainsKey(key))
                        {
                            freq[key] = 0;
                            seqBytesStore[key] = concat.ToArray();
                        }
                        freq[key]++;
                    }
                }
            }

            // score: prefer longer byte sequences that occur frequently
            var scored = freq.Select(kv => new
            {
                Key = kv.Key,
                Bytes = seqBytesStore[kv.Key],
                Freq = kv.Value,
                Score = seqBytesStore[kv.Key].Length * kv.Value
            })
            .Where(x => x.Freq >= 2) // at least two occurrences
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Freq)
            .Take(maxCandidates)
            .ToList();

            var result = new List<int[]>();
            foreach (var item in scored)
            {
                var ints = DecodeTrieSequenceToInts(item.Bytes, useZigZag);
                result.Add(ints.ToArray());
            }
            return result;
        }

        // Build mapped sequences consisting of single top keys (old-style behavior but as sequences length=1)
        private static List<int[]> BuildSingleKeyMappedSeqs(int[,] matrix, int maxCandidates = MAX_MAPPED)
        {
            var freq = new Dictionary<int, int>();
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    int v = matrix[r, c];
                    if (!freq.ContainsKey(v)) freq[v] = 0;
                    freq[v]++;
                }

            var mappedKeys = freq
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key)
                .Select(kv => kv.Key)
                .Take(maxCandidates)
                .OrderBy(x => x)
                .ToList();

            var res = new List<int[]>();
            foreach (var k in mappedKeys) res.Add(new int[] { k });
            return res;
        }

        // Save matrix indices using mapped sequences (mappedSeqs) and matching trie.
        // This writes: rows(int32), cols(int32), then nibble-stream with either index or RAW markers.
        public static void SaveMatrixAsIndices_V2(BinaryWriter writer, int[,] matrix, List<int[]> mappedSeqs, ByteTrie trieForMatching, bool useZigZag = true)
        {
            if (mappedSeqs.Count > MAX_MAPPED)
                throw new ArgumentException($"mappedSeqs.Count must be <= {MAX_MAPPED}");

            // Build seqBytes for mappedSeqs
            var seqBytesList = new List<byte[]>();
            for (int i = 0; i < mappedSeqs.Count; i++)
            {
                var list = new List<byte>();
                foreach (var v in mappedSeqs[i]) list.AddRange(EncodeIntForTrie(v, useZigZag));
                seqBytesList.Add(list.ToArray());
            }

            Dictionary<string, int> bytesToIndex = new Dictionary<string, int>();
            for (int i = 0; i < seqBytesList.Count; i++)
                bytesToIndex[Convert.ToBase64String(seqBytesList[i])] = i;

            int rows = matrix.GetLength(0), cols = matrix.GetLength(1);

            writer.Write(rows);
            writer.Write(cols);

            int currentByte = 0;
            int bitPos = 0;
            Action<int> AppendNibble = (nib) =>
            {
                currentByte |= (nib & 0x0F) << bitPos;
                bitPos += 4;
                if (bitPos == 8)
                {
                    writer.Write((byte)currentByte);
                    currentByte = 0;
                    bitPos = 0;
                }
            };

            for (int r = 0; r < rows; r++)
            {
                int c = 0;
                while (c < cols)
                {
                    // try longest match in trie starting at (r,c)
                    ByteTrieNode node = trieForMatching.Root;
                    int scanC = c;
                    int lastMatchIndex = -1;
                    int lastMatchCells = 0;
                    var tempBytes = new List<byte>();

                    while (scanC < cols)
                    {
                        byte[] enc = EncodeIntForTrie(matrix[r, scanC], useZigZag);
                        bool failed = false;
                        foreach (var b in enc)
                        {
                            if (!node.Children.TryGetValue(b, out ByteTrieNode child))
                            {
                                failed = true;
                                break;
                            }
                            node = child;
                            tempBytes.Add(b);
                            if (node.IsLeaf)
                            {
                                string key = Convert.ToBase64String(tempBytes.ToArray());
                                if (bytesToIndex.TryGetValue(key, out int idx))
                                {
                                    lastMatchIndex = idx;
                                    lastMatchCells = (scanC - c + 1);
                                }
                            }
                        }
                        if (failed) break;
                        scanC++;
                    }

                    if (lastMatchIndex >= 0)
                    {
                        AppendNibble(lastMatchIndex);
                        c += lastMatchCells;
                    }
                    else
                    {
                        // RAW for single cell (current c)
                        AppendNibble(RESERVED_RAW_NIBBLE);
                        // little-endian raw bytes, minimal length
                        byte[] full = BitConverter.GetBytes(matrix[r, c]); // little-endian
                        int useBytes = 4;
                        while (useBytes > 1 && full[useBytes - 1] == 0) useBytes--;
                        AppendNibble(useBytes - 1); // 0..3
                        for (int b = 0; b < useBytes; b++)
                        {
                            byte by = full[b];
                            AppendNibble(by & 0x0F);
                            AppendNibble((by >> 4) & 0x0F);
                        }
                        c++;
                    }
                }
            }

            if (bitPos != 0) writer.Write((byte)currentByte);
        }

        // Load matrix from indices when mappedSeqs (sequences) are used.
        public static int[,] LoadMatrixFromIndices_V2(BinaryReader reader, List<int[]> mappedSeqs)
        {
            if (mappedSeqs == null) throw new ArgumentNullException(nameof(mappedSeqs));
            if (mappedSeqs.Count > MAX_MAPPED) throw new ArgumentException($"mappedSeqs.Count must be <= {MAX_MAPPED}");

            int rows = reader.ReadInt32();
            int cols = reader.ReadInt32();

            int[,] matrix = new int[rows, cols];

            bool hasByte = false;
            byte currentByte = 0;
            int nibbleOffset = 0; // 0 or 4

            Func<int> ReadNibble = () =>
            {
                if (!hasByte)
                {
                    int b = reader.ReadByte();
                    currentByte = (byte)b;
                    hasByte = true;
                    nibbleOffset = 0;
                }

                int nibble = (currentByte >> nibbleOffset) & 0x0F;
                nibbleOffset += 4;
                if (nibbleOffset == 8)
                {
                    hasByte = false;
                    nibbleOffset = 0;
                }
                return nibble;
            };

            for (int r = 0; r < rows; r++)
            {
                int c = 0;
                while (c < cols)
                {
                    int nib = ReadNibble();
                    if (nib == RESERVED_RAW_NIBBLE)
                    {
                        int lenNib = ReadNibble();
                        int useBytes = lenNib + 1; // 1..4
                        byte[] bytes = new byte[useBytes];
                        for (int b = 0; b < useBytes; b++)
                        {
                            int low = ReadNibble();
                            int high = ReadNibble();
                            bytes[b] = (byte)(low | (high << 4));
                        }
                        byte[] full = new byte[4];
                        Array.Copy(bytes, 0, full, 0, useBytes);
                        int val = BitConverter.ToInt32(full, 0);
                        matrix[r, c] = val;
                        c++;
                    }
                    else
                    {
                        if (nib < mappedSeqs.Count)
                        {
                            var seq = mappedSeqs[nib];
                            int len = seq.Length;
                            if (c + len > cols)
                                throw new InvalidDataException("Sequence exceeds row bounds during decoding.");
                            for (int k = 0; k < len; k++)
                                matrix[r, c + k] = seq[k];
                            c += len;
                        }
                        else
                        {
                            throw new InvalidDataException($"Encountered index {nib} but only {mappedSeqs.Count} mapped sequences are present.");
                        }
                    }
                }
            }

            return matrix;
        }

        // Сериализация byte[] с картой до MAX_MAPPED наиболее частых байтов (аналог SaveMatrixAsIndices, но для байтов)
        public static void SaveBytesAsIndices(BinaryWriter writer, byte[] data, List<int> mappedBytes)
        {
            if (mappedBytes.Count > MAX_MAPPED)
                throw new ArgumentException($"mappedBytes.Count must be <= {MAX_MAPPED}");

            Dictionary<int, int> valueToIndex = new Dictionary<int, int>();
            for (int i = 0; i < mappedBytes.Count; i++)
                valueToIndex[mappedBytes[i]] = i;

            int currentByte = 0;
            int bitPos = 0;

            Action<int> AppendNibble = (nib) =>
            {
                currentByte |= (nib & 0x0F) << bitPos;
                bitPos += 4;
                if (bitPos == 8)
                {
                    writer.Write((byte)currentByte);
                    currentByte = 0;
                    bitPos = 0;
                }
            };

            for (int i = 0; i < data.Length; i++)
            {
                int v = data[i]; // 0..255
                if (valueToIndex.TryGetValue(v, out int idx))
                {
                    AppendNibble(idx);
                }
                else
                {
                    AppendNibble(RESERVED_RAW_NIBBLE);
                    byte by = (byte)v;
                    AppendNibble(by & 0x0F);
                    AppendNibble((by >> 4) & 0x0F);
                }
            }

            if (bitPos != 0)
                writer.Write((byte)currentByte);
        }

        public static byte[] LoadBytesFromIndices(BinaryReader reader, List<int> mappedBytes, long expectedLengthHint = -1)
        {
            if (mappedBytes == null) throw new ArgumentNullException(nameof(mappedBytes));
            if (mappedBytes.Count > MAX_MAPPED) throw new ArgumentException($"mappedBytes.Count must be <= {MAX_MAPPED}");

            bool hasByte = false;
            byte currentByte = 0;
            int nibbleOffset = 0; // 0 or 4

            Func<int> ReadNibble = () =>
            {
                if (!hasByte)
                {
                    int b = reader.ReadByte();
                    currentByte = (byte)b;
                    hasByte = true;
                    nibbleOffset = 0;
                }

                int nibble = (currentByte >> nibbleOffset) & 0x0F;
                nibbleOffset += 4;
                if (nibbleOffset == 8)
                {
                    hasByte = false;
                    nibbleOffset = 0;
                }
                return nibble;
            };

            var list = new List<byte>();

            try
            {
                while (true)
                {
                    int nib = ReadNibble();
                    if (nib == RESERVED_RAW_NIBBLE)
                    {
                        int low = ReadNibble();
                        int high = ReadNibble();
                        byte by = (byte)(low | (high << 4));
                        list.Add(by);
                    }
                    else
                    {
                        if (nib < mappedBytes.Count)
                            list.Add((byte)mappedBytes[nib]);
                        else
                            throw new InvalidDataException($"Encountered index {nib} but only {mappedBytes.Count} mapped bytes are present.");
                    }
                }
            }
            catch (EndOfStreamException)
            {
                // закончено чтение
            }

            return list.ToArray();
        }

        // ----- New: serialize dynamic block (either using single-key mappedSeqs or multi-cell sequences) -----
        // if useSequences==false => mappedSeqs are single-key sequences (old-like)
        private static byte[] SerializeDynamicBlock(int[,] matrix, bool useSequences)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);

            bool useZigZag = true;
            int maxCellsSeq = 4;

            List<int[]> mappedSeqs;
            if (useSequences)
                mappedSeqs = BuildTopSequencesFromMatrix(matrix, maxCellsSeq, MAX_MAPPED, useZigZag);
            else
                mappedSeqs = BuildSingleKeyMappedSeqs(matrix, MAX_MAPPED);

            using (var msOut = new MemoryStream())
            using (var bw = new BinaryWriter(msOut, Encoding.Default, true))
            {
                // 1) write mappedSeqs table
                bw.Write((byte)mappedSeqs.Count);
                foreach (var seq in mappedSeqs)
                {
                    if (seq.Length > 255) throw new InvalidOperationException("Sequence too long to store length in one byte.");
                    bw.Write((byte)seq.Length);
                    foreach (var v in seq) bw.Write(v);
                }

                // 2) build trie1 and save
                ByteTrie trie1 = new ByteTrie();
                foreach (var seq in mappedSeqs)
                {
                    var bytesList = new List<byte>();
                    foreach (var v in seq) bytesList.AddRange(EncodeIntForTrie(v, useZigZag));
                    trie1.AddBytesDirect(bytesList.ToArray());
                }
                trie1.SaveTreeUltraCompact(bw);

                // 3) INT-DELTA
                bool hasNegative = false;
                int minVal = int.MaxValue;
                for (int i = 0; i < rows; i++)
                    for (int j = 0; j < cols; j++)
                    {
                        int v = matrix[i, j];
                        if (v < 0) hasNegative = true;
                        if (v < minVal) minVal = v;
                    }

                bool intDeltaPresent = (!hasNegative) && (minVal > 0);
                int intDelta = intDeltaPresent ? minVal : 0;

                bw.Write((byte)(intDeltaPresent ? 1 : 0));
                if (intDeltaPresent) bw.Write(intDelta);

                // 4) matrixToWrite
                int[,] matrixToWrite = matrix;
                if (intDeltaPresent)
                {
                    matrixToWrite = new int[rows, cols];
                    for (int i = 0; i < rows; i++)
                        for (int j = 0; j < cols; j++)
                            matrixToWrite[i, j] = matrix[i, j] - intDelta;
                }

                // 5) serialize matrixToWrite into bytes using mappedSeqs & trie1
                byte[] matrixDataBytes;
                using (var ms = new MemoryStream())
                using (var bw2 = new BinaryWriter(ms, Encoding.Default, true))
                {
                    SaveMatrixAsIndices_V2(bw2, matrixToWrite, mappedSeqs, trie1, useZigZag);
                    bw2.Flush();
                    matrixDataBytes = ms.ToArray();
                }

                // 6) BYTE-DELTA
                int minByte = 255;
                if (matrixDataBytes.Length == 0) minByte = 0;
                else
                {
                    foreach (var b in matrixDataBytes)
                        if (b < minByte) minByte = b;
                }

                bool byteDeltaPresent = (minByte > 0);
                byte byteDelta = (byte)(byteDeltaPresent ? minByte : 0);

                byte[] shiftedBytes;
                if (byteDeltaPresent)
                {
                    shiftedBytes = new byte[matrixDataBytes.Length];
                    for (int i = 0; i < matrixDataBytes.Length; i++)
                        shiftedBytes[i] = (byte)(matrixDataBytes[i] - byteDelta);
                }
                else
                {
                    shiftedBytes = matrixDataBytes;
                }

                // 7) mappedBytes for stage2
                var byteFreq = new Dictionary<int, int>();
                foreach (var b in shiftedBytes)
                {
                    int vb = b;
                    if (!byteFreq.ContainsKey(vb)) byteFreq[vb] = 0;
                    byteFreq[vb]++;
                }

                var mappedBytes = byteFreq
                    .OrderByDescending(kv => kv.Value)
                    .ThenBy(kv => kv.Key)
                    .Select(kv => kv.Key)
                    .Take(MAX_MAPPED)
                    .OrderBy(x => x)
                    .ToList();

                ByteTrie trie2 = new ByteTrie();
                foreach (var b in mappedBytes) trie2.AddBytesDirect(new byte[] { (byte)b });

                // 8) evaluate stage2
                byte[] stage2Data;
                using (var ms = new MemoryStream())
                using (var bw2 = new BinaryWriter(ms, Encoding.Default, true))
                {
                    trie2.SaveTreeUltraCompact(bw2);
                    SaveBytesAsIndices(bw2, shiftedBytes, mappedBytes);
                    bw2.Flush();
                    stage2Data = ms.ToArray();
                }

                bool useSecondStage = stage2Data.Length < matrixDataBytes.Length;

                bw.Write((byte)(useSecondStage ? 1 : 0));

                if (useSecondStage)
                {
                    bw.Write((byte)(byteDeltaPresent ? 1 : 0));
                    if (byteDeltaPresent) bw.Write(byteDelta);
                    bw.Write(stage2Data);
                }
                else
                {
                    bw.Write(matrixDataBytes);
                }

                bw.Flush();
                return msOut.ToArray();
            }
        }

        // --- Top-level SaveMatrix chooses between single-key variant and sequence variant ---
        public static void SaveMatrix(BinaryWriter writer, int[,] matrix)
        {
            // 0 => single-key mappedSeqs (old-like)
            // 1 => multi-cell sequence mappedSeqs (new)
            byte[] singleBlock = SerializeDynamicBlock(matrix, useSequences: false);
            byte[] seqBlock = SerializeDynamicBlock(matrix, useSequences: true);

            bool pickSeq = seqBlock.Length < singleBlock.Length;
            if (pickSeq)
            {
                writer.Write((byte)1); // version flag: 1 = sequence variant
                writer.Write(seqBlock);
            }
            else
            {
                writer.Write((byte)0); // version flag: 0 = single-key variant
                writer.Write(singleBlock);
            }
        }

        // ----- LoadMatrix reads version flag first, then decodes the block (both variants use same block structure) -----
        public static int[,] LoadMatrix(BinaryReader reader)
        {
            // Read version flag (0 = single-key variant, 1 = sequence variant)
            int versionFlag = reader.ReadByte();
            if (versionFlag != 0 && versionFlag != 1)
                throw new InvalidDataException("Unknown matrix format version flag.");

            // After this, the block format is the same: first mappedCount, then mappedSeqs table, then trie1, then int-delta, then stage2 decision/data
            // So we can proceed exactly as before with dynamic loader

            // 1) Прочитаем таблицу mappedSeqs
            int mappedCount = reader.ReadByte();
            var mappedSeqs = new List<int[]>();
            for (int i = 0; i < mappedCount; i++)
            {
                int len = reader.ReadByte();
                int[] seq = new int[len];
                for (int j = 0; j < len; j++) seq[j] = reader.ReadInt32();
                mappedSeqs.Add(seq);
            }

            // 2) Загрузим trie1 (структуру)
            ByteTrie loadedTrie1 = new ByteTrie();
            loadedTrie1.LoadTreeUltraCompact(reader);

            // 3) Прочитаем INT-DELTA флаг и значение
            int intDelta = 0;
            bool intDeltaPresent = false;
            {
                int flag = reader.ReadByte();
                if (flag != 0)
                {
                    intDeltaPresent = true;
                    intDelta = reader.ReadInt32();
                }
            }

            // 4) Флаг использования второго этапа
            bool useSecondStage = (reader.ReadByte() != 0);

            byte[] matrixDataBytes;

            if (useSecondStage)
            {
                byte byteDelta = 0;
                bool byteDeltaPresent = false;
                {
                    int flag = reader.ReadByte();
                    if (flag != 0)
                    {
                        byteDeltaPresent = true;
                        byteDelta = reader.ReadByte();
                    }
                }

                ByteTrie loadedTrie2 = new ByteTrie();
                loadedTrie2.LoadTreeUltraCompact(reader);
                // collect mapped bytes from trie2 (DFS)
                List<int> loadedMappedBytes = new List<int>();
                if (loadedTrie2 != null && loadedTrie2.Root != null)
                {
                    var stack = new Stack<(ByteTrieNode node, List<byte> path)>();
                    stack.Push((loadedTrie2.Root, new List<byte>()));
                    while (stack.Count > 0)
                    {
                        var pair = stack.Pop();
                        var node = pair.node;
                        var path = pair.path;

                        if (node.IsLeaf && path.Count > 0)
                        {
                            loadedMappedBytes.Add(path[0]);
                        }

                        foreach (var child in node.Children.Values)
                        {
                            var newPath = new List<byte>(path) { child.Value };
                            stack.Push((child, newPath));
                        }
                    }

                    loadedMappedBytes = loadedMappedBytes.Distinct().OrderBy(x => x).ToList();
                }

                long left = reader.BaseStream.Length - reader.BaseStream.Position;
                if (left <= 0)
                    throw new EndOfStreamException("No bytes left when expected encoded matrix block.");

                byte[] remaining = reader.ReadBytes((int)left);
                using (var ms = new MemoryStream(remaining))
                using (var br = new BinaryReader(ms, Encoding.Default, true))
                {
                    byte[] decodedShiftedBytes = LoadBytesFromIndices(br, loadedMappedBytes);

                    if (byteDeltaPresent)
                    {
                        matrixDataBytes = new byte[decodedShiftedBytes.Length];
                        for (int i = 0; i < decodedShiftedBytes.Length; i++)
                            matrixDataBytes[i] = (byte)(decodedShiftedBytes[i] + byteDelta);
                    }
                    else
                    {
                        matrixDataBytes = decodedShiftedBytes;
                    }
                }
            }
            else
            {
                long left = reader.BaseStream.Length - reader.BaseStream.Position;
                if (left < 0) left = 0;
                matrixDataBytes = reader.ReadBytes((int)left);
            }

            // 5) Декодируем матрицу из matrixDataBytes
            int[,] loadedMatrix;
            using (var msDecoded = new MemoryStream(matrixDataBytes))
            using (var brMatrix = new BinaryReader(msDecoded))
            {
                loadedMatrix = LoadMatrixFromIndices_V2(brMatrix, mappedSeqs);
            }

            // 6) Применяем обратно int-delta
            if (intDeltaPresent && intDelta != 0)
            {
                int rows = loadedMatrix.GetLength(0);
                int cols = loadedMatrix.GetLength(1);
                for (int i = 0; i < rows; i++)
                    for (int j = 0; j < cols; j++)
                        loadedMatrix[i, j] = loadedMatrix[i, j] + intDelta;
            }

            return loadedMatrix;
        }
    }
}
