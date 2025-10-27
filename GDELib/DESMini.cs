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

        public void Add(int number)
        {
            byte[] bytes = BitConverter.GetBytes(number);
            Array.Reverse(bytes);
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

        // Получить все числа из дерева
        public List<int> GetAllNumbers()
        {
            List<int> numbers = new List<int>();
            CollectNumbers(Root, new List<byte>(), numbers);
            return numbers;
        }

        private void CollectNumbers(ByteTrieNode node, List<byte> currentBytes, List<int> numbers)
        {
            if (node.IsLeaf && currentBytes.Count > 0)
            {
                byte[] bytes = currentBytes.ToArray();
                Array.Reverse(bytes); // Восстанавливаем оригинальный порядок байт
                if (bytes.Length == 4) // int состоит из 4 байт
                {
                    int number = BitConverter.ToInt32(bytes, 0);
                    numbers.Add(number);
                }
            }

            foreach (var child in node.Children.Values)
            {
                currentBytes.Add(child.Value);
                CollectNumbers(child, currentBytes, numbers);
                currentBytes.RemoveAt(currentBytes.Count - 1);
            }
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
        const int RESERVED_RAW_NIBBLE = 0xE;

        public static void SaveMatrixAsIndices(BinaryWriter writer, int[,] matrix, List<int> mappedKeys)
        {
            if (mappedKeys.Count > MAX_MAPPED)
                throw new ArgumentException($"mappedKeys.Count must be <= {MAX_MAPPED}");

            Dictionary<int, int> valueToIndex = new Dictionary<int, int>();
            for (int i = 0; i < mappedKeys.Count; i++)
                valueToIndex[mappedKeys[i]] = i;

            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);

            writer.Write(rows);
            writer.Write(cols);

            // Буфер для упаковки нибблов
            int currentByte = 0;
            int bitPos = 0; // 0 или 4

            // Записать ниббл (0..15) в поток
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

            int rowsTotal = matrix.GetLength(0);
            int colsTotal = matrix.GetLength(1);

            for (int i = 0; i < rowsTotal; i++)
            {
                for (int j = 0; j < colsTotal; j++)
                {
                    int value = matrix[i, j];
                    if (valueToIndex.TryGetValue(value, out int idx))
                    {
                        // Записываем 4-битный индекс
                        AppendNibble(idx);
                    }
                    else
                    {
                        // Пишем маркер RAW
                        AppendNibble(RESERVED_RAW_NIBBLE);

                        // Подготовим минимальное количество байт для little-endian представления
                        byte[] full = BitConverter.GetBytes(value); // little-endian
                        int useBytes = 4;
                        // убираем старшие нули
                        while (useBytes > 1 && full[useBytes - 1] == 0)
                            useBytes--;

                        // Записываем (len-1) в следующий ниббл (0..3)
                        AppendNibble(useBytes - 1);

                        // Записываем сами байты (little-endian), каждый байт как два ниббла
                        for (int b = 0; b < useBytes; b++)
                        {
                            byte by = full[b];
                            AppendNibble(by & 0x0F);
                            AppendNibble((by >> 4) & 0x0F);
                        }
                    }
                }
            }

            // Дописываем неполный байт, если есть
            if (bitPos != 0)
            {
                writer.Write((byte)currentByte);
            }
        }

        public static int[,] LoadMatrixFromIndices(BinaryReader reader, List<int> mappedKeys)
        {
            if (mappedKeys == null) throw new ArgumentNullException(nameof(mappedKeys));
            if (mappedKeys.Count > MAX_MAPPED) throw new ArgumentException($"mappedKeys.Count must be <= {MAX_MAPPED}");

            int rows = reader.ReadInt32();
            int cols = reader.ReadInt32();

            int[,] matrix = new int[rows, cols];

            // Буфер для чтения нибблов
            bool hasByte = false;
            byte currentByte = 0;
            int nibbleOffset = 0; // 0 или 4

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

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    int nib = ReadNibble();
                    if (nib == RESERVED_RAW_NIBBLE)
                    {
                        // читаем ниббл длины
                        int lenNib = ReadNibble();
                        int useBytes = lenNib + 1; // 1..4

                        byte[] bytes = new byte[useBytes];
                        for (int b = 0; b < useBytes; b++)
                        {
                            int low = ReadNibble();
                            int high = ReadNibble();
                            bytes[b] = (byte)(low | (high << 4));
                        }

                        // дополняем до 4 байт (старшие байты = 0), затем восстановим int
                        byte[] full = new byte[4];
                        Array.Copy(bytes, 0, full, 0, useBytes);
                        int val = BitConverter.ToInt32(full, 0);
                        matrix[i, j] = val;
                    }
                    else
                    {
                        if (nib < mappedKeys.Count)
                        {
                            matrix[i, j] = mappedKeys[nib];
                        }
                        else
                        {
                            throw new InvalidDataException($"Encountered index {nib} but only {mappedKeys.Count} mapped keys are present.");
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

            // Буфер нибблов
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
                    // RAW marker
                    AppendNibble(RESERVED_RAW_NIBBLE);
                    // Для байта: сразу запишем сам байт как два ниббла (low, high)
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

            // Функция чтения ниббла из потока
            bool hasByte = false;
            byte currentByte = 0;
            int nibbleOffset = 0; // 0 или 4

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

            // Если мы знаем точную длину блока — можно читать строго столько значений.
            // Но в общем случае вызывающий код должен знать когда остановиться (например, по длине матрицы).
            // Здесь будем читать до тех пор, пока не вывалимся из потока — вызывающий код должен оборачивать в MemoryStream/ограниченный поток.
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
                // закончили чтение — нормально
            }

            return list.ToArray();
        }


        public static void SaveMatrix(BinaryWriter writer, int[,] matrix)
        {
            // 1) Собираем частоты значений
            var freq = new Dictionary<int, int>();
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                {
                    int v = matrix[i, j];
                    if (!freq.ContainsKey(v)) freq[v] = 0;
                    freq[v]++;
                }

            // 2) Выбираем mappedKeys (до MAX_MAPPED)
            var mappedKeys = freq
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key)
                .Select(kv => kv.Key)
                .Take(MAX_MAPPED)
                .OrderBy(x => x)
                .ToList();

            // 3) Строим trie1 и сохраняем его
            ByteTrie trie1 = new ByteTrie();
            foreach (var k in mappedKeys) trie1.Add(k);
            trie1.SaveTreeUltraCompact(writer);

            // 4) INT-DELTA: решаем, применять ли сдвиг по минимуму
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

            writer.Write((byte)(intDeltaPresent ? 1 : 0));
            if (intDeltaPresent) writer.Write(intDelta);

            // 5) Подготовим трансформированную матрицу
            int[,] matrixToWrite = matrix;
            if (intDeltaPresent)
            {
                matrixToWrite = new int[rows, cols];
                for (int i = 0; i < rows; i++)
                    for (int j = 0; j < cols; j++)
                        matrixToWrite[i, j] = matrix[i, j] - intDelta;
            }

            // 6) Сериализуем matrixToWrite в байтовый буфер
            byte[] matrixDataBytes;
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.Default, true))
            {
                SaveMatrixAsIndices(bw, matrixToWrite, mappedKeys);
                bw.Flush();
                matrixDataBytes = ms.ToArray();
            }

            // 7) BYTE-DELTA: найти минимальный байт
            int minByte = 255;
            if (matrixDataBytes.Length == 0) minByte = 0;
            else
            {
                foreach (var b in matrixDataBytes)
                    if (b < minByte) minByte = b;
            }

            bool byteDeltaPresent = (minByte > 0);
            byte byteDelta = (byte)(byteDeltaPresent ? minByte : 0);

            // 8) Подготовка данных для второго этапа
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

            // Собираем частоты байтов и строим mappedBytes для trie2
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
            foreach (var b in mappedBytes) trie2.Add(b);

            // 9) Оцениваем размер после второго этапа сжатия
            byte[] stage2Data;
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.Default, true))
            {
                trie2.SaveTreeUltraCompact(bw);
                SaveBytesAsIndices(bw, shiftedBytes, mappedBytes);
                bw.Flush();
                stage2Data = ms.ToArray();
            }

            // 10) Сравниваем размеры и решаем, применять ли второй этап
            bool useSecondStage = stage2Data.Length < matrixDataBytes.Length;

            // Записываем флаг использования второго этапа
            writer.Write((byte)(useSecondStage ? 1 : 0));

            if (useSecondStage)
            {
                // Второй этап выгоден - используем его
                writer.Write((byte)(byteDeltaPresent ? 1 : 0));
                if (byteDeltaPresent) writer.Write(byteDelta);
                writer.Write(stage2Data);
            }
            else
            {
                // Второй этап невыгоден - записываем исходные данные
                writer.Write(matrixDataBytes);
            }
        }

        public static int[,] LoadMatrix(BinaryReader reader)
        {
            // 1) Загрузим trie1
            ByteTrie loadedTrie1 = new ByteTrie();
            loadedTrie1.LoadTreeUltraCompact(reader);
            List<int> loadedMappedKeys = loadedTrie1.GetAllNumbers().OrderBy(x => x).ToList();

            // 2) Прочитаем INT-DELTA флаг и значение
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

            // 3) Прочитаем флаг использования второго этапа
            bool useSecondStage = (reader.ReadByte() != 0);

            byte[] matrixDataBytes;

            if (useSecondStage)
            {
                // 4) Второй этап использовался - читаем BYTE-DELTA
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

                // 5) Загрузим trie2
                ByteTrie loadedTrie2 = new ByteTrie();
                loadedTrie2.LoadTreeUltraCompact(reader);
                List<int> loadedMappedBytes = loadedTrie2.GetAllNumbers().OrderBy(x => x).ToList();

                // 6) Прочитаем и декодируем данные второго этапа
                long left = reader.BaseStream.Length - reader.BaseStream.Position;
                if (left <= 0)
                    throw new EndOfStreamException("No bytes left when expected encoded matrix block.");

                byte[] remaining = reader.ReadBytes((int)left);
                using (var ms = new MemoryStream(remaining))
                using (var br = new BinaryReader(ms, Encoding.Default, true))
                {
                    byte[] decodedShiftedBytes = LoadBytesFromIndices(br, loadedMappedBytes);

                    // Применяем обратно byte-delta
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
                // 7) Второй этап не использовался - читаем исходные данные
                long left = reader.BaseStream.Length - reader.BaseStream.Position;
                if (left < 0) left = 0;
                matrixDataBytes = reader.ReadBytes((int)left);
            }

            // 8) Декодируем матрицу из данных
            int[,] loadedMatrix;
            using (var msDecoded = new MemoryStream(matrixDataBytes))
            using (var brMatrix = new BinaryReader(msDecoded))
            {
                loadedMatrix = LoadMatrixFromIndices(brMatrix, loadedMappedKeys);
            }

            // 9) Применяем обратно int-delta
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
