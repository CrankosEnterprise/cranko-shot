using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using ShotMind.Core.Models;

namespace ShotMind.Core.Services;

public class HistoryService
{
    private static readonly string DbPath;
    private static readonly string ConnectionString;

    private static HistoryService? _instance;
    public static HistoryService Instance => _instance ??= new HistoryService();

    private bool _isInitialized = false;

    static HistoryService()
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "ShotMind");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            DbPath = Path.Combine(folder, "history.db");
            ConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = DbPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();
        }
        catch (Exception ex)
        {
            Logger.Error("Erro ao preparar caminho do banco de histórico.", ex);
            DbPath = "history.db";
            ConnectionString = "Data Source=history.db";
        }
    }

    private HistoryService()
    {
    }

    private static void EnsureDbDirectoryExists()
    {
        try
        {
            string? dir = Path.GetDirectoryName(DbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        catch { }
    }

    public async Task InitializeAsync()
    {
        try
        {
            EnsureDbDirectoryExists();
            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            string sql = @"
                CREATE TABLE IF NOT EXISTS Captures (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    FilePath TEXT NOT NULL,
                    FileName TEXT NOT NULL,
                    Width INTEGER NOT NULL,
                    Height INTEGER NOT NULL,
                    Format TEXT NOT NULL,
                    ThumbnailBase64 TEXT NOT NULL,
                    OcrText TEXT,
                    AiSummary TEXT
                );
                CREATE INDEX IF NOT EXISTS idx_captures_timestamp ON Captures(Timestamp);
            ";

            using var command = new SqliteCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
            _isInitialized = true;
            Logger.Info("Banco de dados SQLite de histórico inicializado com sucesso.");
        }
        catch (Exception ex)
        {
            Logger.Error("Erro ao inicializar banco de dados SQLite de histórico.", ex);
        }
    }

    public static string GenerateThumbnailBase64(Bitmap bitmap, int maxDim = 160)
    {
        try
        {
            int w = bitmap.Width;
            int h = bitmap.Height;
            if (w <= 0 || h <= 0) return string.Empty;

            double ratio = Math.Min((double)maxDim / w, (double)maxDim / h);
            if (ratio > 1.0) ratio = 1.0;

            int targetW = Math.Max(1, (int)(w * ratio));
            int targetH = Math.Max(1, (int)(h * ratio));

            using var thumb = new Bitmap(targetW, targetH);
            using (var g = Graphics.FromImage(thumb))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                g.DrawImage(bitmap, 0, 0, targetW, targetH);
            }

            byte[] bytes = ScreenCaptureService.ToJpgByteArray(thumb, 75);
            return Convert.ToBase64String(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task<long> AddCaptureAsync(HistoryItem item)
    {
        try
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }
            EnsureDbDirectoryExists();
            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            string sql = @"
                INSERT INTO Captures (Timestamp, FilePath, FileName, Width, Height, Format, ThumbnailBase64, OcrText, AiSummary)
                VALUES (@ts, @path, @name, @w, @h, @fmt, @thumb, @ocr, @ai);
                SELECT last_insert_rowid();
            ";

            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@ts", item.Timestamp.ToString("o"));
            cmd.Parameters.AddWithValue("@path", item.FilePath);
            cmd.Parameters.AddWithValue("@name", item.FileName);
            cmd.Parameters.AddWithValue("@w", item.Width);
            cmd.Parameters.AddWithValue("@h", item.Height);
            cmd.Parameters.AddWithValue("@fmt", item.Format);
            cmd.Parameters.AddWithValue("@thumb", item.ThumbnailBase64 ?? string.Empty);
            cmd.Parameters.AddWithValue("@ocr", (object?)item.OcrText ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ai", (object?)item.AiSummary ?? DBNull.Value);

            var result = await cmd.ExecuteScalarAsync();
            long id = Convert.ToInt64(result);
            item.Id = id;
            return id;
        }
        catch (Exception ex)
        {
            Logger.Error("Erro ao registrar captura no histórico SQLite.", ex);
            return -1;
        }
    }

    public async Task<List<HistoryItem>> GetCapturesAsync(string? searchTerm = null)
    {
        var list = new List<HistoryItem>();
        try
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }
            EnsureDbDirectoryExists();
            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            string sql = "SELECT Id, Timestamp, FilePath, FileName, Width, Height, Format, ThumbnailBase64, OcrText, AiSummary FROM Captures ";
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                sql += "WHERE FileName LIKE @search OR OcrText LIKE @search OR AiSummary LIKE @search ";
            }
            sql += "ORDER BY Timestamp DESC LIMIT 100;";

            using var cmd = new SqliteCommand(sql, connection);
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                cmd.Parameters.AddWithValue("@search", $"%{searchTerm}%");
            }

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var item = new HistoryItem
                {
                    Id = reader.GetInt64(0),
                    Timestamp = DateTime.Parse(reader.GetString(1)),
                    FilePath = reader.GetString(2),
                    FileName = reader.GetString(3),
                    Width = reader.GetInt32(4),
                    Height = reader.GetInt32(5),
                    Format = reader.GetString(6),
                    ThumbnailBase64 = reader.GetString(7),
                    OcrText = reader.IsDBNull(8) ? null : reader.GetString(8),
                    AiSummary = reader.IsDBNull(9) ? null : reader.GetString(9)
                };
                list.Add(item);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Erro ao buscar histórico SQLite.", ex);
        }

        return list;
    }

    public async Task<bool> DeleteCaptureAsync(long id)
    {
        try
        {
            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            string sql = "DELETE FROM Captures WHERE Id = @id;";
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"Erro ao excluir item de histórico {id}.", ex);
            return false;
        }
    }

    public async Task<bool> ClearHistoryAsync(bool deletePhysicalFiles)
    {
        try
        {
            if (deletePhysicalFiles)
            {
                var all = await GetCapturesAsync();
                foreach (var item in all)
                {
                    try
                    {
                        if (File.Exists(item.FilePath))
                        {
                            File.Delete(item.FilePath);
                        }
                    }
                    catch { }
                }
            }

            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            string sql = "DELETE FROM Captures;";
            using var cmd = new SqliteCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("Erro ao limpar histórico.", ex);
            return false;
        }
    }
}
