using System;
using System.IO;
using System.Text;
using Shinzui.Application.Interfaces.Inventory;
using UnityEngine;

namespace Shinzui.Infrastructure.SaveSystem
{
    /// <summary>
    /// スロット単位のJSONまたはバイナリファイルを読み書きする
    /// </summary>
    public sealed class SaveFileStore
    {
        public const int SlotCount = 3;
        private const int Magic = 0x535A5549;
        private readonly string _directory;
        private readonly bool _json;

        /// <summary>実行環境に対応する保存先と形式を選択する</summary>
        public SaveFileStore() : this(Path.Combine(UnityEngine.Application.persistentDataPath, "Saves"),
#if UNITY_EDITOR
            true
#else
            false
#endif
        ) { }

        /// <summary>保存先と形式を指定する</summary>
        /// <param name="directory">保存フォルダ</param>
        /// <param name="json">JSON形式を使用するか</param>
        public SaveFileStore(string directory, bool json)
        {
            _directory = directory;
            _json = json;
        }

        /// <summary>スロットの保存ファイルパスを取得する</summary>
        /// <param name="slot">スロット番号</param>
        /// <returns>保存ファイルの絶対パス</returns>
        public string GetPath(int slot)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            return Path.Combine(_directory, $"slot_{slot}.{(_json ? "json" : "bin")}");
        }

        /// <summary>保存データの存在を確認する</summary>
        /// <param name="slot">スロット番号</param>
        /// <returns>ファイルが存在するか</returns>
        public bool Exists(int slot) => File.Exists(GetPath(slot));

        /// <summary>一時ファイルを経由して保存データを確定する</summary>
        /// <param name="slot">スロット番号</param>
        /// <param name="data">保存する持ち物</param>
        public void Save(int slot, GameSaveData data)
        {
            Validate(data);
            var path = GetPath(slot);
            Directory.CreateDirectory(_directory);
            var temporary = path + ".tmp";

            // 書き込みが完了してから既存ファイルを置き換える
            if (_json) File.WriteAllText(temporary, JsonUtility.ToJson(data, true), Encoding.UTF8);
            else
            {
                using var writer = new BinaryWriter(File.Create(temporary), Encoding.UTF8);
                writer.Write(Magic);
                writer.Write(data.Version);
                writer.Write(data.InventorySaveData.EquippedSlotIndex);
                writer.Write(data.InventorySaveData.Slots.Length);
                foreach (var item in data.InventorySaveData.Slots)
                {
                    writer.Write(item.SlotIndex);
                    writer.Write(item.ItemId);
                    writer.Write(item.Quantity);
                }
                writer.Write(data.SpecialItemSaveData.ItemId ?? "");
            }

            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        /// <summary>保存ファイルを読み込み形式と内容を検証する</summary>
        /// <param name="slot">スロット番号</param>
        /// <returns>保存された持ち物</returns>
        public GameSaveData Load(int slot)
        {
            var path = GetPath(slot);
            GameSaveData data;
            if (_json) data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path, Encoding.UTF8));
            else
            {
                using var reader = new BinaryReader(File.OpenRead(path), Encoding.UTF8);
                if (reader.ReadInt32() != Magic) throw new InvalidDataException("セーブファイルの形式が不正");
                data = new GameSaveData { Version = reader.ReadInt32() };
                data.InventorySaveData.EquippedSlotIndex = reader.ReadInt32();
                var count = reader.ReadInt32();
                if (count < 0 || count > 1024) throw new InvalidDataException("スロット数が不正");
                data.InventorySaveData.Slots = new InventorySlotSaveData[count];
                for (var i = 0; i < count; i++)
                {
                    data.InventorySaveData.Slots[i] = new InventorySlotSaveData
                    {
                        SlotIndex = reader.ReadInt32(), ItemId = reader.ReadString(), Quantity = reader.ReadInt32()
                    };
                }
                data.SpecialItemSaveData.ItemId = reader.ReadString();
                if (reader.BaseStream.Position != reader.BaseStream.Length)
                    throw new InvalidDataException("セーブファイルに余分なデータが存在");
            }

            Validate(data);
            return data;
        }

        /// <summary>指定スロットの保存ファイルを削除する</summary>
        /// <param name="slot">スロット番号</param>
        public void Delete(int slot) => File.Delete(GetPath(slot));

        /// <summary>保存形式と持ち物の基本的な整合性を検証する</summary>
        /// <param name="data">検証対象</param>
        private static void Validate(GameSaveData data)
        {
            if (data == null || data.Version != 1 || data.InventorySaveData?.Slots == null ||
                data.SpecialItemSaveData == null || data.InventorySaveData.EquippedSlotIndex < -1)
                throw new InvalidDataException("未対応または破損したセーブデータ");

            var indices = new System.Collections.Generic.HashSet<int>();
            foreach (var item in data.InventorySaveData.Slots)
            {
                if (item == null || item.SlotIndex < 0 || item.Quantity <= 0 ||
                    string.IsNullOrEmpty(item.ItemId) || !indices.Add(item.SlotIndex))
                    throw new InvalidDataException("持ち物の保存データが不正");
            }
            if (data.InventorySaveData.EquippedSlotIndex >= 0 &&
                !indices.Contains(data.InventorySaveData.EquippedSlotIndex))
                throw new InvalidDataException("装備スロットが存在しない");
        }
    }
}
