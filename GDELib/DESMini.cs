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
        // Максимум ключей, которые можно кодировать в 4 бита.
        // Резервируем ниббл 0xE (14) как маркер "далее идёт int32 в явном виде".
        public const int MAX_MAPPED = 14;
        const int RESERVED_RAW_NIBBLE = 0xE;

        // Записываем матрицу, используя заранее заданные mappedKeys (порядок важен).
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


        public static void SaveMatrix(BinaryWriter writer, int[,] matrix)
        {
            // Собираем частоты значений
            var freq = new Dictionary<int, int>();
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    int v = matrix[i, j];
                    if (!freq.ContainsKey(v)) freq[v] = 0;
                    freq[v]++;
                }
            }

            // Выбираем до MAX_MAPPED наиболее частых значений.
            var mappedKeys = freq
                .OrderByDescending(kv => kv.Value)    // сначала по частоте
                .ThenBy(kv => kv.Key)                 // при равной частоте — по значению
                .Select(kv => kv.Key)
                .Take(MAX_MAPPED)
                .OrderBy(x => x) // детерминированный порядок по значению для стабильного соответствия индексов
                .ToList();

            // Строим trie только из mappedKeys (не пишем их отдельно)
            ByteTrie trie = new ByteTrie();
            foreach (var k in mappedKeys)
                trie.Add(k);

            // Сохраняем trie (ультра-компактный формат)
            trie.SaveTreeUltraCompact(writer);

            // Сохраняем саму матрицу как индексы, пользуясь mappedKeys (порядок должен быть восстановим из trie при чтении)
            SaveMatrixAsIndices(writer, matrix, mappedKeys);
        }

        public static int[,] LoadMatrix(BinaryReader reader)
        {
            // Загружаем trie
            ByteTrie loadedTrie = new ByteTrie();
            loadedTrie.LoadTreeUltraCompact(reader);

            // Восстанавливаем mappedKeys из trie (детерминированно — сортируем по значению)
            List<int> loadedMappedKeys = loadedTrie.GetAllNumbers().OrderBy(x => x).ToList();

            // Загружаем матрицу как индексы, передавая known mapped keys
            int[,] loadedMatrix = LoadMatrixFromIndices(reader, loadedMappedKeys);

            return loadedMatrix;
        }
    }
}
